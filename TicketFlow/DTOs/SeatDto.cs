using TicketFlow.Models;

namespace TicketFlow.DTOs;

public class SeatDto
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string Row { get; set; } = string.Empty;
    public int Number { get; set; }
    public decimal Price { get; set; }
    public bool IsBooked { get; set; }
    public int? BookingId { get; set; }
}
