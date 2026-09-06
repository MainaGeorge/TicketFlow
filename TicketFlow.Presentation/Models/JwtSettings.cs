namespace TicketFlow.Presentation.Models;

internal class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
}
