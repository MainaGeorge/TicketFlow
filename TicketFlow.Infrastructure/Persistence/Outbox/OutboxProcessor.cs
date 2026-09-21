using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TicketFlow.Application.ApplicationEvents;
using TicketFlow.Domain.Common;

namespace TicketFlow.Infrastructure.Persistence.Outbox;

public class OutboxProcessor(AppDbContext context, IDomainEventDispatcher dispatcher, ILogger<OutboxProcessor> logger, IOptions<OutboxOptions> options)
{
    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        var processingBatch = await context
            .OutboxMessages
            .Where(m => m.ProcessedAt == null && m.FailedAt == null)
            .OrderBy(m => m.OccurredAt)
            .ThenBy(m => m.Id)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var message in processingBatch)
        {
            try
            {
                var eventType = typeof(IDomainEvent).Assembly.GetType(message.Type);

                if (eventType is null || !typeof(IDomainEvent).IsAssignableFrom(eventType))
                    throw new InvalidOperationException($"Could not resolve domain event type '{message.Type}'.");

                var deserialized = JsonSerializer.Deserialize(message.Payload, eventType);

                if (deserialized is not IDomainEvent domainEvent)
                    throw new InvalidOperationException($"Could not deserialize domain event '{message.Type}'.");

                await dispatcher.DispatchAsync([domainEvent], cancellationToken);

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
}
