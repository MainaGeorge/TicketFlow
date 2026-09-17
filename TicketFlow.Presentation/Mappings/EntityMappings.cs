using TicketFlow.Application.Bookings.Models;
using TicketFlow.Application.Events.Models;
using TicketFlow.Application.Seats;
using TicketFlow.Contracts.DTOs;
using TicketFlow.Domain.Entities;

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

    public static EventDto MapToEventDto(this EventResult result)
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

    public static SeatDto MapToSeatDto(this Seat result)
    {
        return new SeatDto
        {
            BookingId = result.Booking?.Id,
            EventId = result.EventId,
            Id = result.Id,
            IsBooked = result.Booking != null,
            Number = result.Number,
            Price = result.Price,
            Row = result.Row
        };
    }

    public static RegisterUserResponseDto MapToRegisterUserDto(this User user)
    {
        return new RegisterUserResponseDto { Email = user.Email!, Id = user.Id, UserName = user.DisplayName};
    }
}
