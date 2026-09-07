using System.ComponentModel.DataAnnotations;

namespace TicketFlow.Contracts.DTOs;

public class CreateBookingRequest
{
    [Range(1, int.MaxValue)]
    public required int? SeatId { get; init; }
}
