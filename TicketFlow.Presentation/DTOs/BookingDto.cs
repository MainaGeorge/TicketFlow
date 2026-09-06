namespace TicketFlow.Presentation.DTOs;

public class BookingDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int SeatId { get; set; }
    public int EventId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public string SeatRow { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public decimal Price { get; set; }
}
