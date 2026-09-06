namespace TicketFlow.Application.Bookings;

public interface IBookingService
{
    Task<BookingBaseResult> CreateBookingAsync(string userId, int seatId, CancellationToken cancellationToken = default);
    Task<BookingResult?> GetBookingAsync(int bookingId, string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<BookingResult?>> GetBookingsAsync(string userId, CancellationToken cancellationToken = default);
}
