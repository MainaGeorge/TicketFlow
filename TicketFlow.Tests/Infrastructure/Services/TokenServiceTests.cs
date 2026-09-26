using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TicketFlow.Application.Authorization;
using TicketFlow.Application.Common.Configurations;
using TicketFlow.Domain.Entities;
using TicketFlow.Infrastructure.Authentication;

namespace TicketFlow.Tests.Infrastructure.Services;

public class TokenServiceTests
{

    [Fact]
    public async Task GenerateTokensAsync_WhenUserIsValid_ReturnsTokensAndPersistsRefreshToken()
    {
        var jwtSettings = new JwtSettings
        {
            Key = "very-complex-string-long-enough-for-hmacsha256",
            Issuer = "TicketFlow",
            Audience = "TicketFlowApi",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeMinutes = 10080
        };

        var jwtOptions = Options.Create(jwtSettings);
        var logger = new Mock<ILogger<TokenService>>();
        var service = new TokenService(logger.Object, jwtOptions);

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "test@email.com",
            UserName = "test@email.com"
        };

        var result = await service.GenerateTokensAsync(user, [], CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.Equal("Bearer", result.TokenType);
        Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GenerateTokensAsync_WhenUserIsValid_ContainsExpectedClaims()
    {
        var jwtSettings = new JwtSettings
        {
            Key = "very-complex-string-long-enough-for-hmacsha256",
            Issuer = "TicketFlow",
            Audience = "TicketFlowApi",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeMinutes = 10080
        };

        var jwtOptions = Options.Create(jwtSettings);
        var logger = new Mock<ILogger<TokenService>>();
        var service = new TokenService(logger.Object, jwtOptions);

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "test@email.com",
            UserName = "test@email.com"
        };

        List<string> rolesCollection = [Roles.User, Roles.Admin];

        var result = await service.GenerateTokensAsync(user, rolesCollection, CancellationToken.None);
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);

        Assert.Equal(user.Id, token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(user.Id, token.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(user.UserName, token.Claims.First(c => c.Type == ClaimTypes.Name).Value);

        var roleClaims = token.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Equal(2, roleClaims.Count);
        Assert.Contains(Roles.User, roleClaims);
        Assert.Contains(Roles.Admin, roleClaims);
    }
}
