namespace TicketFlow.DTOs;

public class CreateEventRequest
{
    public string Name { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateTime EventDate { get; set; } = DateTime.UtcNow;
}
