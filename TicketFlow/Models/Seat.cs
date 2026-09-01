namespace TicketFlow.Models;

public class Seat
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public string Row { get; set; } = string.Empty;
    public int Number { get; set; }
    public decimal Price { get; set; }
    public bool IsBooked { get; set; }
    public virtual Booking Booking { get; set; } = null!;
    public int BookingId { get; set; }
}
