using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TicketFlow.Application.Abstractions.Authentication;
using TicketFlow.Application.Common.Configurations;
using TicketFlow.Contracts.Authentication;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Authentication;

public class TokenService(ILogger<TokenService> logger, IOptions<JwtSettings> jwtOptions) : ITokenService
{
    private readonly JwtSettings _jwtSettings = jwtOptions.Value;
    private string GenerateAccessTokens(User user, IEnumerable<string> roles, DateTimeOffset expiresAt)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty)
        };

        var roleClaims = roles.Select(r => new Claim(ClaimTypes.Role, r));
        claims.AddRange(roleClaims);

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    public Task<TokenResponse> GenerateTokensAsync(User user, IEnumerable<string> roles, CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.AccessTokenLifetimeMinutes);
        var accessToken = GenerateAccessTokens(user, roles, expiresAt);
        var refreshToken = GenerateRefreshToken();

        logger.LogInformation("Generated access and refresh tokens for user {Email}.", user.Email);

        var tokens = new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            TokenType = "Bearer"
        };

        return Task.FromResult(tokens);
    }
}
