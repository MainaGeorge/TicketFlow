using Microsoft.Extensions.Options;
using System.Text;
using TicketFlow.Application.Common.Configurations;

namespace TicketFlow.Application.Common.Validators;


public sealed class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    public ValidateOptionsResult Validate(string? name, JwtSettings settings)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(settings.Key))
            errors.Add(JwtSettingsValidationMessages.MissingKey);
        else if (Encoding.UTF8.GetByteCount(settings.Key) < 32)
            errors.Add(JwtSettingsValidationMessages.MinimumKeyLength);

        if (string.IsNullOrWhiteSpace(settings.Issuer))
            errors.Add(JwtSettingsValidationMessages.MissingIssuer);

        if (string.IsNullOrWhiteSpace(settings.Audience))
            errors.Add(JwtSettingsValidationMessages.MissingAudience);

        if (settings.AccessTokenLifetimeMinutes <= 0)
            errors.Add(JwtSettingsValidationMessages.InvalidAccessTokenLifetime);

        if (settings.RefreshTokenLifetimeMinutes <= 0)
            errors.Add(JwtSettingsValidationMessages.InvalidRefreshTokenLifetime);

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
