using System.ComponentModel.DataAnnotations;

namespace TicketFlow.Contracts.DTOs;

public class CreateEventRequest
{
    [Required]
    [MaxLength(200)]
    public string? Name { get; set; }

    [Required]
    [MaxLength(200)]
    public string? Venue { get; set; }

    [Required]
    public DateTime? EventDate { get; set; }
}
