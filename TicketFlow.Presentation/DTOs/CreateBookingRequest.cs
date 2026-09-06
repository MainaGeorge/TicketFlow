using System.ComponentModel.DataAnnotations;

namespace TicketFlow.Presentation.DTOs;

public class CreateBookingRequest
{
    [Range(1, int.MaxValue)]
    public required int? SeatId { get; init; }
}
