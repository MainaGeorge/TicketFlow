using Microsoft.Extensions.Logging;
using TicketFlow.Application.Bookings.Exceptions;
using TicketFlow.Application.Bookings.Interfaces;
using TicketFlow.Application.Bookings.Models;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Bookings.Services;

public class BookingsService(IBookingRepository bookingRepository, ILogger<BookingsService> logger) : IBookingService
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
            return new BookingCreated(booking);
        }
        catch (SeatAlreadyBookedException)
        {
            logger.LogError("Seat booked by another user. SeatId: {SeatId}", seatId);
            return new BookingSeatAlreadyBooked();
        }
    }

    public async Task<BookingBaseResult?> GetBookingAsync(int bookingId, string userId, CancellationToken cancellationToken = default)
    {
        var booking = await bookingRepository.GetBookingAsync(bookingId, userId, cancellationToken);

        if(booking is null)
            return new BookingNotFound();

        return new BookingResult(booking);
    }

    public async Task<IEnumerable<BookingResult>> GetBookingsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var bookings =  await bookingRepository.GetUserBookingsAsync(userId, cancellationToken);

        return bookings.Select(b => new BookingResult(b));
    }
}
