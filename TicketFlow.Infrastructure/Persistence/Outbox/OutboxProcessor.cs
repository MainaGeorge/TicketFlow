using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;
using TicketFlow.Application.Abstractions.Messaging;
using TicketFlow.Application.ApplicationEvents;
using TicketFlow.Contracts.IntegrationEvents;
using TicketFlow.Domain.Common;
using TicketFlow.Infrastructure.Observability;

namespace TicketFlow.Infrastructure.Persistence.Outbox;

public class OutboxProcessor(
    AppDbContext context,
    IDomainEventDispatcher dispatcher,
    IIntegrationEventPublisher integrationEventPublisher,
    ILogger<OutboxProcessor> logger,
    IOptions<OutboxOptions> options)
{
    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        var processingBatch = await context
            .OutboxMessages
            .Where(m => m.ProcessedAt == null && m.FailedAt == null)
            .OrderBy(m => m.OccurredAt)
            .ThenBy(m => m.Id)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in processingBatch)
        {
            var hasParentContext = ActivityContext.TryParse(message.TraceParent, message.TraceState, true, out var parentContext);

            using var activity = hasParentContext
                ? TicketFlowTelemetry.ActivitySource.StartActivity("Outbox.Process", ActivityKind.Internal, parentContext)
                : TicketFlowTelemetry.ActivitySource.StartActivity("Outbox.Process", ActivityKind.Internal);

            activity?.SetTag("outbox.message.id", message.Id);
            activity?.SetTag("outbox.message.type", message.Type);
            activity?.SetTag("outbox.message.category", message.MessageType.ToString());
            activity?.SetTag("outbox.retry_count", message.RetryCount);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                switch (message.MessageType)
                {
                    case OutboxMessageType.DomainEvent:
                        await ProcessDomainEventAsync(message, cancellationToken);
                        break;

                    case OutboxMessageType.IntegrationEvent:
                        await ProcessIntegrationEventAsync(message, cancellationToken);
                        break;

                    default:
                        throw new InvalidOperationException($"Unsupported outbox message type '{message.MessageType}'.");
                }

                message.MarkProcessed();
                TicketFlowTelemetry.OutboxProcessed.Add(1);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                activity?.AddException(exception);

                TicketFlowTelemetry.OutboxProcessingFailures.Add(1);

                logger.LogError(
                    exception,
                    "Failed to process outbox message {OutboxMessageId} of type {OutboxMessageType}",
                    message.Id,
                    message.Type);

                message.RecordFailure();

                if (message.RetryCount >= options.Value.MaxRetryAttempts)
                    message.MarkFailed();
            }
            finally
            {
                try
                {
                    await context.SaveChangesAsync(cancellationToken);
                }
                finally
                {
                    stopwatch.Stop();
                    TicketFlowTelemetry.OutboxProcessingDuration.Record(stopwatch.Elapsed.TotalMilliseconds);
                }
            }
        }
    }

    private async Task ProcessDomainEventAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var eventType = typeof(IDomainEvent).Assembly.GetType(message.Type);

        if (eventType is null || !typeof(IDomainEvent).IsAssignableFrom(eventType))
            throw new InvalidOperationException($"Could not resolve domain event type '{message.Type}'.");

        var deserialized = JsonSerializer.Deserialize(message.Payload, eventType);

        if (deserialized is not IDomainEvent domainEvent)
            throw new InvalidOperationException($"Could not deserialize domain event '{message.Type}'.");

        await dispatcher.DispatchAsync([domainEvent], cancellationToken);
    }

    private async Task ProcessIntegrationEventAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var eventType = typeof(BookingCreatedIntegrationEvent).Assembly.GetType(message.Type)
            ?? throw new InvalidOperationException($"Could not resolve integration event type '{message.Type}'.");

        var integrationEvent = JsonSerializer.Deserialize(message.Payload, eventType)
            ?? throw new InvalidOperationException($"Could not deserialize integration event '{message.Type}'.");

        await integrationEventPublisher.PublishAsync(integrationEvent, cancellationToken);
    }
}
