namespace TicketFlow.Application.Common.Validators;

public static class JwtSettingsValidationMessages
{
    public const string MissingKey = "JwtSettings Key is required";
    public const string MinimumKeyLength = "JwtSettings Key must be at least 32 bytes";
    public const string MissingIssuer = "JwtSettings Issuer is required.";
    public const string MissingAudience = "JwtSettings Audience is required.";
    public const string InvalidAccessTokenLifetime = "JwtSettings AccessTokenLifetimeMinutes must be greater than zero.";
    public const string InvalidRefreshTokenLifetime = "JwtSettings AccessTokenLifetimeMinutes must be greater than zero.";
}