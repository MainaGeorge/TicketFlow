using System.ComponentModel.DataAnnotations;

namespace TicketFlow.Contracts.Booking;

public class CreateBookingRequest
{
    [Range(1, int.MaxValue)]
    public required int SeatId { get; init; }

    [Range(1, int.MaxValue)]
    public required int EventId { get; init; }
}
