using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Bookings.Interfaces;

public interface IBookingRepository
{
    Task<Seat?> GetSeatForBookingAsync(int seatId, CancellationToken cancellationToken = default);
    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(int seatId, CancellationToken cancellationToken = default);
    Task<Booking?> GetBookingAsync(int bookingId, string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Booking>> GetUserBookingsAsync(string userId, CancellationToken cancellationToken = default);
}
