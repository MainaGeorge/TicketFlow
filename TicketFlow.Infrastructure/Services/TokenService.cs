using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TicketFlow.Application.Authentication.Interfaces;
using TicketFlow.Contracts.DTOs;
using TicketFlow.Domain.Entities;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Infrastructure.Services;

public class TokenService(AppDbContext appDbContext, IConfiguration configuration, ILogger<TokenService> logger) : ITokenService
{
    private string GenerateAccessTokens(User user, DateTimeOffset expiresAt)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");

        var key = jwtSettings["Key"]!;
        var issuer = jwtSettings["Issuer"]!;
        var audience = jwtSettings["Audience"]!;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty)
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
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

    public async Task<TokenResponse> GenerateTokensAsync(User user, CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        var accessToken = GenerateAccessTokens(user, expiresAt);
        var refreshToken = GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow
        };

        appDbContext.RefreshTokens.Add(refreshTokenEntity);

        logger.LogInformation("Generated access and refresh tokens for user {Email}.", user.Email);

        await appDbContext.SaveChangesAsync(cancellationToken);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            TokenType = "Bearer"
        };
    }
}
