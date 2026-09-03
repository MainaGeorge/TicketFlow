using TicketFlow.Models;

namespace TicketFlow.DTOs;

public class BookingDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int SeatId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
}
