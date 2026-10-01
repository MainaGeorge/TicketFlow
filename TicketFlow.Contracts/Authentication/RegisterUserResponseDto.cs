namespace TicketFlow.Contracts.Authentication;

public class RegisterUserResponseDto
{
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}
