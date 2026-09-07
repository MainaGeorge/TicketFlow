using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Events;

public interface IEventRepository
{
    Task<Event?> GetEventAsync(int eventId, CancellationToken cancellationToken = default);
    Task<Event> CreateEventAsync(Event @event, CancellationToken cancellationToken = default);
    Task<IEnumerable<Event>> GetAllEventsAsync(CancellationToken cancellationToken = default);
}
