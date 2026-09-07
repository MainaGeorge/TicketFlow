using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Bookings;
using TicketFlow.Application.Bookings.Exceptions;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence.Repositories;

internal class BookingRepository(AppDbContext context) : IBookingRepository
{
    public async Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        await context.Bookings.AddAsync(booking, cancellationToken);
    }

    public async Task<Booking?> GetBookingAsync(int bookingId, string userId, CancellationToken cancellationToken = default)
    {
        return await context.Bookings
            .Where(b => b.Id == bookingId && b.UserId == userId)
            .Select(b => new Booking
            { 
                Seat = b.Seat,
                Id = b.Id,
                CreatedAt = b.CreatedAt,
                PaymentReference = b.PaymentReference,
                UserId = b.UserId,
            })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public async Task<Seat?> GetSeatForBookingAsync(int seatId, CancellationToken cancellationToken = default)
    {
        return 
            await context
            .Seats
            .Include(s => s.Event)
            .Include(s => s.Booking)
            .FirstOrDefaultAsync(s => s.Id == seatId, cancellationToken);
    }

    public async Task<IEnumerable<Booking>> GetUserBookingsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await context.Bookings
            .Where(b => b.UserId == userId)
            .Select(b => new Booking
            {
                Seat = b.Seat,
                Id = b.Id,
                CreatedAt = b.CreatedAt,
                PaymentReference = b.PaymentReference,
                UserId = b.UserId,
            })
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task SaveChangesAsync(int seatId, CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 2601 or 2627)
        {
            throw new SeatAlreadyBookedException(seatId);
        }
    }
}
