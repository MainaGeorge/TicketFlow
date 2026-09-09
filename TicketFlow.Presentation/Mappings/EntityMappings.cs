using TicketFlow.Application.Bookings.Models;
using TicketFlow.Application.Events.Models;
using TicketFlow.Application.Seats;
using TicketFlow.Contracts.DTOs;

namespace TicketFlow.Presentation.Mappings;

public static class EntityMappings
{
    public static BookingDto MapToBookingDto(this BookingBaseResult result, string userId)
    {
        return new BookingDto
        {
            Id = result.Booking!.Id,
            UserId = userId,
            SeatId = result.Booking.Seat!.Id,
            CreatedAt = result.Booking!.CreatedAt,
            EventId = result.Booking!.Seat!.EventId,
            SeatRow = result.Booking.Seat.Row,
            SeatNumber = result.Booking.Seat.Number,
            Price = result.Booking.Seat.Price
        };
    }

    public static EventDto MapToEventDto(this EventBaseResult result)
    {
        return new EventDto
        {
            AvailableSeats = result.Event!.AvailableSeats,
            EventDate = result.Event.EventDate,
            Id = result.Event.Id,
            Name = result.Event.Name,
            TotalSeats = result.Event!.TotalSeats,
            Venue = result.Event!.Venue,
        };
    }

    public static SeatDto MapToSeatDto(this SeatBaseResult result)
    {
        return new SeatDto
        {
            BookingId = result.Seat!.Booking?.Id,
            EventId = result.Seat.EventId,
            Id = result.Seat.Id,
            IsBooked = result.Seat.Booking != null,
            Number = result.Seat.Number,
            Price = result.Seat.Price,
            Row = result.Seat.Row
        };
    }
}
