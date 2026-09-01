using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TicketFlow.Data;
using TicketFlow.Models;

namespace TicketFlow.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthenticationController(IConfiguration configuration, UserManager<User> userManager, AppDbContext appDbContext) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] DTOs.RegisterRequest registrationDto)
    {
        var existingUser = await userManager.FindByEmailAsync(registrationDto.Email);

        if (existingUser is not null)
            return BadRequest(new { Message = $"User with this email '{registrationDto.Email}' already exists" });

        var user = new User
        {
            UserName = registrationDto.Email,
            Email = registrationDto.Email,
            IsActive = true,
            DisplayName = registrationDto.DisplayName
        };

        var result = await userManager.CreateAsync(user, registrationDto.Password);

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return Ok(new { Message = "User registered successfully" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(DTOs.LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user == null)
            return Unauthorized();

        if (!user.IsActive)
            return Unauthorized("Account is inactive.");

        var validPassword = await userManager.CheckPasswordAsync(user, request.Password);

        if (!validPassword)
            return Unauthorized();

        var tokens = await GenerateTokensAsync(user);

        return Ok(tokens);
    }

    private string GenerateAccessToken(User user, DateTimeOffset expiresAt)
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
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    private async Task<TokenResponse> GenerateTokensAsync(User user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        var accessToken = GenerateAccessToken(user, expiresAt);

        var refreshToken = GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow
        };

        appDbContext.RefreshTokens.Add(refreshTokenEntity);

        await appDbContext.SaveChangesAsync();

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            TokenType = "Bearer"
        };
    }
}
