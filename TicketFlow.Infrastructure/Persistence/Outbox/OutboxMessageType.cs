namespace TicketFlow.Infrastructure.Persistence.Outbox;

public enum OutboxMessageType
{
    DomainEvent = 1,
    IntegrationEvent = 2,
    Unknown = 0
}
