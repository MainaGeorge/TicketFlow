using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Abstractions.Repositories;
using TicketFlow.Application.Seats.Exceptions;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence.Repositories;

public class SeatsRepository(AppDbContext context) : ISeatsRepository
{
    public async Task<Seat> CreateSeatAsync(Seat seat, CancellationToken cancellationToken = default)
    {
        await context.Seats.AddAsync(seat, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return seat;
    }

    public async Task<IEnumerable<Seat>> CreateSeatsAsync(IEnumerable<Seat> seats, CancellationToken cancellationToken = default)
    {
        context.Seats.AddRange(seats);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 2601 or 2627)
        {
            throw new DuplicateSeatException();
        }

        return seats;
    }

    public async Task<Seat?> GetSeatAsync(int eventId, int seatId, CancellationToken cancellationToken = default)
    {
        return await context
            .Seats
            .Include(b => b.Booking)
            .FirstOrDefaultAsync(s => s.Id == seatId && s.EventId == eventId, cancellationToken);
    }

    public async Task<IEnumerable<Seat>> GetSeatsAsync(int eventId, CancellationToken cancellationToken = default)
    {
        return await context
            .Seats
            .Include(b => b.Booking)
            .Where(s => s.EventId == eventId)
            .ToListAsync(cancellationToken);
    }
}
