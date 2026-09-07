using System.ComponentModel.DataAnnotations;

namespace TicketFlow.Contracts.DTOs;

public class DeactivateUserRequest : BaseEmailRequest
{
}


public class  ReactivateUserRequest:BaseEmailRequest
{
}


public class BaseEmailRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;
}
