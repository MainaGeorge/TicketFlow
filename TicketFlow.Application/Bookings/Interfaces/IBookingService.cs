using TicketFlow.Application.Bookings.Models;

namespace TicketFlow.Application.Bookings.Interfaces;

public interface IBookingService
{
    Task<BookingBaseResult> CreateBookingAsync(string userId, int seatId, CancellationToken cancellationToken = default);
    Task<BookingBaseResult?> GetBookingAsync(int bookingId, string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<BookingResult>> GetBookingsAsync(string userId, CancellationToken cancellationToken = default);
}
