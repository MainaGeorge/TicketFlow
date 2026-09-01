namespace TicketFlow.DTOs;

public class CreateBookingRequest
{
    public DateTime CreatedAt => DateTime.UtcNow;
    public string PaymentReference { get; set; } = string.Empty;
    public required int SeatId { get; init; }
    public required string UserId { get; init; }
}
