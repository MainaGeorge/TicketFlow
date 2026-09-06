using Microsoft.Extensions.Logging;
using TicketFlow.Application.Bookings.Exceptions;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Bookings;

public class BookingService(IBookingRepository bookingRepository, ILogger<BookingService> logger) : IBookingService
{
    public async Task<BookingBaseResult> CreateBookingAsync(string userId, int seatId, CancellationToken cancellationToken)
    {
        var seat = await bookingRepository.GetSeatForBookingAsync(seatId, cancellationToken);

        if (seat is null)
        {
            logger.LogWarning("Seat not found. SeatId: {SeatId}", seatId);
            return new BookingSeatNotFound();
        }

        if (seat.Booking is not null)
        {
            logger.LogWarning("Seat is already booked. SeatId: {SeatId}", seatId);
            return new BookingSeatAlreadyBooked();
        }

        if (seat.Event.EventDate < DateTime.UtcNow)
        {
            logger.LogWarning("Event is unavailable. SeatId: {SeatId}", seatId);
            return new BookingEventUnavailable();
        }

        var booking = new Booking
        {
            UserId = userId,
            SeatId = seatId,
            CreatedAt = DateTime.UtcNow
        };

        await bookingRepository.AddAsync(booking, cancellationToken);

        try
        {
            await bookingRepository.SaveChangesAsync(seatId, cancellationToken);
            return new BookingCreated(userId, booking.Id, booking.CreatedAt, seat);
        }
        catch (SeatAlreadyBookedException)
        {
            return new BookingSeatAlreadyBooked();
        }
    }

    public Task<BookingResult?> GetBookingAsync(int bookingId, string userId, CancellationToken cancellationToken = default)
    {
        return bookingRepository.GetBookingAsync(bookingId, userId, cancellationToken);
    }

    public Task<IEnumerable<BookingResult?>> GetBookingsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return bookingRepository.GetUserBookingsAsync(userId, cancellationToken);
    }
}
