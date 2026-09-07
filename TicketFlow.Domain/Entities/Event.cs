using System.ComponentModel.DataAnnotations.Schema;

namespace TicketFlow.Domain.Entities;

public class Event
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public ICollection<Seat> Seats { get; set; } = [];

    [NotMapped]
    public int TotalSeats { get; set; }

    [NotMapped]
    public int AvailableSeats { get; set; }
}
