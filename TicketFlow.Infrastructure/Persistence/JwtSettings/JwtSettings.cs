namespace TicketFlow.Infrastructure.Persistence.JwtSettings;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";
    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; init; } = 15;
    public int RefreshTokenLifetimeMinutes { get; init; } = 120;
}
