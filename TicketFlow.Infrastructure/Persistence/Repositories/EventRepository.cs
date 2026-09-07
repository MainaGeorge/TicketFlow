using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Events;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence.Repositories;

public class EventRepository(AppDbContext context) : IEventRepository
{
    public async Task<Event> CreateEventAsync(Event @event, CancellationToken cancellationToken = default)
    {
        context.Events.Add(@event);
        await context.SaveChangesAsync(cancellationToken);

        return @event;
    }

    public async Task<IEnumerable<Event>> GetAllEventsAsync(CancellationToken cancellationToken = default)
    {
        return await context
            .Events
            .Select(e => new Event 
                {  
                    AvailableSeats = e.Seats.Count(s => s.Booking != null),
                    TotalSeats = e.Seats.Count,
                    EventDate = e.EventDate,
                    Id = e.Id,
                    Name = e.Name,
                    Venue = e.Venue,
                })
            .ToListAsync(cancellationToken);
    }
    public async Task<Event?> GetEventAsync(int eventId, CancellationToken cancellationToken = default)
    {
        return await context
            .Events
            .Select(e => new Event
            {
                AvailableSeats = e.Seats.Count(s => s.Booking != null),
                TotalSeats = e.Seats.Count,
                EventDate = e.EventDate,
                Id = e.Id,
                Name = e.Name,
                Venue = e.Venue,
            })
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);
    }
}
