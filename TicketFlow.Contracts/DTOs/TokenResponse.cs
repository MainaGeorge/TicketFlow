namespace TicketFlow.Contracts.DTOs;

public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public string TokenType { get; set; } = "Bearer";
}