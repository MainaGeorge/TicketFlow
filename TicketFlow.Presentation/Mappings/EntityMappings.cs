using TicketFlow.Contracts.DTOs;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Presentation.Mappings;

public static class EntityMappings
{
    public static BookingDto MapToBookingDto(this Booking result, string userId)
    {
        return new BookingDto
        {
            Id = result!.Id,
            UserId = userId,
            SeatId = result.Seat!.Id,
            CreatedAt = result.CreatedAt,
            EventId = result.Seat!.EventId,
            SeatRow = result.Seat.Row,
            SeatNumber = result.Seat.Number,
            Price = result.Seat.Price
        };
    }

    public static EventDto MapToEventDto(this Event result)
    {
        return new EventDto
        {
            AvailableSeats = result.AvailableSeats,
            EventDate = result.EventDate,
            Id = result.Id,
            Name = result.Name,
            TotalSeats = result.TotalSeats,
            Venue = result.Venue,
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
