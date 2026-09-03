using System.ComponentModel.DataAnnotations;

namespace TicketFlow.DTOs;

public class CreateBookingRequest
{
    [Range(1, int.MaxValue)]
    public required int? SeatId { get; init; }
}
