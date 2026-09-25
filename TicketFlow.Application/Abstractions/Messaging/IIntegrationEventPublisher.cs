namespace TicketFlow.Application.Abstractions.Messaging;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(object integrationEvent, CancellationToken cancellationToken = default);
}
