namespace TicketFlow.Models;

public class Booking
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public int SeatId { get; set; }
    public Seat Seat { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
}