using System.ComponentModel.DataAnnotations;

namespace TicketFlow.DTOs;

public class DeactivateUserRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;
}
