using System.Text.Json;
using TicketFlow.Domain.Common;

namespace TicketFlow.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessageFactory
{
    public static OutboxMessage Create(IDomainEvent domainEvent)
    {
        var eventType = domainEvent.GetType();

        var payload = JsonSerializer.Serialize(domainEvent, eventType);

        var type = eventType.FullName ?? throw new InvalidOperationException("Domain event type must have a full name.");

        return new OutboxMessage(type, payload);
    }
}
