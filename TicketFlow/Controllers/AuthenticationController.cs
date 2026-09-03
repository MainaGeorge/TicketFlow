using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TicketFlow.Data;
using TicketFlow.DTOs;
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
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Email already registered.",
                Detail = $"An account with the email address '{registrationDto.Email}' already exists.",
                Instance = HttpContext.Request.Path
            });

        var user = new User
        {
            UserName = registrationDto.Email,
            Email = registrationDto.Email,
            IsActive = true,
            DisplayName = registrationDto.DisplayName
        };

        var result = await userManager.CreateAsync(user, registrationDto.Password);

        if (!result.Succeeded)
            return BadRequest(new ValidationProblemDetails(
                result
                .Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "User Registration Failed.",
                Instance = HttpContext.Request.Path
            });

        return StatusCode(StatusCodes.Status201Created, new {user.Id, user.Email, user.DisplayName });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user == null)
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid credentials.",
                Detail = "The email or password is incorrect.",
                Instance = HttpContext.Request.Path
            });

        if (!user.IsActive)
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Account is inactive.",
                Detail = "The account is not active.",
                Instance = HttpContext.Request.Path
            });

        var validPassword = await userManager.CheckPasswordAsync(user, request.Password);

        if (!validPassword)
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid credentials.",
                Detail = "The email or password is incorrect.",
                Instance = HttpContext.Request.Path
            });

        var tokens = await GenerateTokensAsync(user);

        return Ok(tokens);
    }

    [HttpDelete("deactivate")]
    public async Task<IActionResult> DeactivateAccount(DeactivateUserRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user == null)
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User not found.",
                Detail = "The specified user could not be found.",
                Instance = HttpContext.Request.Path
            });

        user.IsActive = false;
        await appDbContext.SaveChangesAsync();

        return Ok();
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
            expires: expiresAt.UtcDateTime,
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
