using TicketFlow.Domain.Common;

namespace TicketFlow.Application.ApplicationEvents;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}