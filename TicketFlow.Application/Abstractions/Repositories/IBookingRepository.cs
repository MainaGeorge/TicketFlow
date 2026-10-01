using TicketFlow.Application.Bookings.Models;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Abstractions.Repositories;

public interface IBookingRepository
{
    Task<Seat?> GetSeatForBookingAsync(int eventId, int seatId, CancellationToken cancellationToken = default);
    Task<BookingResult?> GetBookingAsync(int bookingId, string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<BookingResult>> GetUserBookingsAsync(string userId, CancellationToken cancellationToken = default);
    Task SaveCreatedBookingAsync(Booking booking, CancellationToken cancellationToken = default);
}
