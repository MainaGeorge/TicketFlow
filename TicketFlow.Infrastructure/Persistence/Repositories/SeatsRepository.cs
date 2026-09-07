using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Seats;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence.Repositories;

public class SeatsRepository(AppDbContext context) : ISeatsRepository
{
    public async Task<Seat> CreateSeatAsync(Seat seat)
    {
        await context.Seats.AddAsync(seat);
        await context.SaveChangesAsync();

        return seat;
    }

    public async Task<Seat?> GetSeatAsync(int eventId, int seatId)
    {
        return await context
            .Seats
            .Include(b => b.Booking)
            .FirstOrDefaultAsync(s => s.Id == seatId && s.EventId == eventId);
    }

    public async Task<IEnumerable<Seat>> GetSeatsAsync(int eventId)
    {
        return await context
            .Seats
            .Include(b => b.Booking)
            .Where(s => s.EventId == eventId)
            .ToListAsync();
    }
}
