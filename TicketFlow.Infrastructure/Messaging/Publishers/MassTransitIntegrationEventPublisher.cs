using MassTransit;
using TicketFlow.Application.Abstractions.Messaging;

namespace TicketFlow.Infrastructure.Messaging.Publishers;

public class MassTransitIntegrationEventPublisher(IPublishEndpoint publishEndpoint) : IIntegrationEventPublisher
{
    public async Task PublishAsync(object integrationEvent, CancellationToken cancellationToken = default)
    {
        await publishEndpoint.Publish(integrationEvent, cancellationToken);
    }
}
