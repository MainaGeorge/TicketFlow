using MediatR;
using TicketFlow.Domain.Common;

namespace TicketFlow.Application.ApplicationEvents;

public sealed class MediatRDomainEventDispatcher(IPublisher publisher) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var domainEventType = domainEvent.GetType();

            var notificationTypeDescriptor = typeof(DomainEventNotification<>).MakeGenericType(domainEventType);

            var notification = Activator.CreateInstance(notificationTypeDescriptor, domainEvent) ??
                throw new InvalidOperationException($"Could not create notification for domain event {domainEvent.GetType().Name}.");

            await publisher.Publish(notification, cancellationToken);
        }
    }
}
