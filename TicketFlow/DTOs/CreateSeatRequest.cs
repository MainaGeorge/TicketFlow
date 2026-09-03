namespace TicketFlow.DTOs;

public class CreateSeatRequest
{
    public string Row { get; set; } = string.Empty;
    public int Number { get; set; }
    public decimal Price { get; set; }
}
