namespace TicketFlow.Application.Messaging;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(object integrationEvent, CancellationToken cancellationToken = default);
}
