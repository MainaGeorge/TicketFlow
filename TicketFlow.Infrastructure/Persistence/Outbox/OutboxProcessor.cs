using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TicketFlow.Application.Abstractions.Messaging;
using TicketFlow.Application.ApplicationEvents;
using TicketFlow.Contracts.IntegrationEvents;
using TicketFlow.Domain.Common;

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
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to process outbox message {OutboxMessageId} of type {OutboxMessageType}",
                    message.Id,
                    message.Type);

                message.RecordFailure();

                if (message.RetryCount >= options.Value.MaxRetryAttempts)
                    message.MarkFailed();
            }

            await context.SaveChangesAsync(cancellationToken);
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
