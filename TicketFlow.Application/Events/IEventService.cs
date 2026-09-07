using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Events;

public interface IEventService
{
    Task<EventBaseResult?> GetEventAsync(int eventId, CancellationToken cancellationToken = default);
    Task<EventBaseResult> CreateEventAsync(Event @event, string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EventResult>> GetAllEventsAsync(CancellationToken cancellationToken = default);
}
