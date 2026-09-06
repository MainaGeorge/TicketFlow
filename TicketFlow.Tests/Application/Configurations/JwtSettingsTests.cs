using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TicketFlow.Application.Common.Configurations;
using TicketFlow.Application.Common.DependencyInjection;
using TicketFlow.Application.Common.Validators;


namespace TicketFlow.Tests.Application.Configurations;

public class JwtSettingsTests
{
    [Fact]
    public void JwtSettings_WhenValid_PassesValidation()
    {
        var settings = new JwtSettings
        {
            Key = "very-complex-string-long-enough-for-hmacsha256",
            Issuer = "TicketFlow",
            Audience = "TicketFlowApi",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeMinutes = 10080
        };

        var validator = new JwtSettingsValidator();

        var result = validator.Validate(Options.DefaultName, settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void JwtSettings_WhenKeyIsMissing_FailsValidation()
    {
        var settings = new JwtSettings
        {
            Key = "",
            Issuer = "TicketFlow",
            Audience = "TicketFlowApi",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeMinutes = 10080
        };

        var validator = new JwtSettingsValidator();

        var result = validator.Validate(Options.DefaultName, settings);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, e => e == JwtSettingsValidationMessages.MissingKey);
    }

    [Fact]
    public void JwtSettings_WhenKeyIsTooShort_FailsValidation()
    {
        var settings = new JwtSettings
        {
            Key = "short",
            Issuer = "TicketFlow",
            Audience = "TicketFlowApi",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeMinutes = 10080
        };

        var validator = new JwtSettingsValidator();

        var result = validator.Validate(Options.DefaultName, settings);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, e => e == JwtSettingsValidationMessages.MinimumKeyLength);
    }

    [Fact]
    public void JwtSettings_WhenIssuerMissing_FailsValidation()
    {
        var settings = new JwtSettings
        {
            Key = "very-complex-string-long-enough-for-hmacsha256",
            Issuer = "",
            Audience = "TicketFlowApi",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeMinutes = 10080
        };

        var validator = new JwtSettingsValidator();

        var result = validator.Validate(Options.DefaultName, settings);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, e => e == JwtSettingsValidationMessages.MissingIssuer);
    }

    [Fact]
    public void JwtSettings_WhenAudienceMissing_FailsValidation()
    {
        var settings = new JwtSettings
        {
            Key = "very-complex-string-long-enough-for-hmacsha256",
            Issuer = "TicketFlow",
            Audience = "",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeMinutes = 10080
        };

        var validator = new JwtSettingsValidator();

        var result = validator.Validate(Options.DefaultName, settings);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, e => e == JwtSettingsValidationMessages.MissingAudience);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void JwtSettings_AccessTokenIsInvalid_FailsValidation(int tokenLifetime)
    {
        var settings = new JwtSettings
        {
            Key = "very-complex-string-long-enough-for-hmacsha256",
            Issuer = "TicketFlow",
            Audience = "",
            AccessTokenLifetimeMinutes = tokenLifetime,
            RefreshTokenLifetimeMinutes = 10080
        };

        var validator = new JwtSettingsValidator();

        var result = validator.Validate(Options.DefaultName, settings);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, e => e == JwtSettingsValidationMessages.InvalidAccessTokenLifetime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void JwtSettings_RefreshTokenIsInvalid_FailsValidation(int tokenLifetime)
    {
        var settings = new JwtSettings
        {
            Key = "very-complex-string-long-enough-for-hmacsha256",
            Issuer = "TicketFlow",
            Audience = "",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeMinutes = tokenLifetime
        };

        var validator = new JwtSettingsValidator();

        var result = validator.Validate(Options.DefaultName, settings);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, e => e == JwtSettingsValidationMessages.InvalidRefreshTokenLifetime);
    }

    [Fact]
    public void JwtSettings_WhenConfigurationIsValid_BindsCorrectly()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Key"] = "very-complex-string-long-enough-for-hmacsha256",
                ["JwtSettings:Issuer"] = "TicketFlow",
                ["JwtSettings:Audience"] = "TicketFlowApi",
                ["JwtSettings:AccessTokenLifetimeMinutes"] = "15",
                ["JwtSettings:RefreshTokenLifetimeMinutes"] = "120"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddJwtSettings(configuration);

        using var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<IOptions<JwtSettings>>().Value;

        Assert.Equal("very-complex-string-long-enough-for-hmacsha256", settings.Key);
        Assert.Equal("TicketFlow", settings.Issuer);
        Assert.Equal("TicketFlowApi", settings.Audience);
        Assert.Equal(15, settings.AccessTokenLifetimeMinutes);
        Assert.Equal(120, settings.RefreshTokenLifetimeMinutes);
    }

    [Fact]
    public void JwtSettings_WhenConfigurationIsInvalid_OptionsValidationFails()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Key"] = "short",
                ["JwtSettings:Issuer"] = "",
                ["JwtSettings:Audience"] = ""
            })
            .Build();

        var services = new ServiceCollection();

        services.AddJwtSettings(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider
                .GetRequiredService<IOptions<JwtSettings>>()
                .Value);

        Assert.Contains(exception.Failures, error => error.Contains("at least 32 bytes"));
        Assert.Contains(exception.Failures, error => error.Contains("Issuer is required"));
        Assert.Contains(exception.Failures, error => error.Contains("Audience is required"));
    }
}
