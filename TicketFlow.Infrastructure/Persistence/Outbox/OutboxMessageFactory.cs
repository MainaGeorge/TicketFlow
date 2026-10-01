using System.Diagnostics;
using System.Text.Json;
using TicketFlow.Domain.Common;

namespace TicketFlow.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessageFactory
{
    public static OutboxMessage CreateDomainEvent(IDomainEvent domainEvent) 
        => Create(domainEvent, OutboxMessageType.DomainEvent);

    public static OutboxMessage CreateIntegrationEvent(object integrationEvent) 
        => Create(integrationEvent, OutboxMessageType.IntegrationEvent);

    private static OutboxMessage Create(object message, OutboxMessageType messageType)
    {
        var type = message.GetType();
        var typeName = type.FullName ?? throw new InvalidOperationException("Outbox message type must have a full name.");
        var payload = JsonSerializer.Serialize(message, type);

        var activity = Activity.Current;

        return new OutboxMessage(typeName, payload, messageType, activity?.Id, activity?.TraceStateString);
    }
}
