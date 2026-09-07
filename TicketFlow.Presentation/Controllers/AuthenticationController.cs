using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TicketFlow.Contracts.DTOs;
using TicketFlow.Domain.Entities;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Presentation.Controllers;

[Route("api/auth")]
[ApiController]
[ApiVersion("1.0")]
public class AuthenticationController(IConfiguration configuration, UserManager<User> userManager, AppDbContext appDbContext, ILogger<AuthenticationController> logger) : ControllerBase
{
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

        var refreshTokenEntity = new Domain.Entities.RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow
        };

        appDbContext.RefreshTokens.Add(refreshTokenEntity);

        logger.LogInformation("Generated access and refresh tokens for user {Email}.", user.Email);

        await appDbContext.SaveChangesAsync();

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            TokenType = "Bearer"
        };
    }

    private IActionResult InvalidCredentials(string email)
    {
        logger.LogInformation("user {Email} attempted to log in but the credentials are invalid.", email);
        return Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Invalid credentials.",
            Detail = "Email or password is incorrect.",
            Instance = HttpContext.Request.Path
        });
    }

    private IActionResult UserNotFound(string email)
    {
        logger.LogInformation("user {Email} attempted to access the system but the account does not exist.", email);

        return NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "User not found.",
            Detail = "The specified user could not be found.",
            Instance = HttpContext.Request.Path
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest registrationDto)
    {
        var existingUser = await userManager.FindByEmailAsync(registrationDto.Email);

        if (existingUser is not null)
        {
            logger.LogInformation("user {Email} attempted to register but the email is already in use.", registrationDto.Email);

            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Email already registered.",
                Detail = $"An account with the email address '{registrationDto.Email}' already exists.",
                Instance = HttpContext.Request.Path
            });
        }

        var user = new Domain.Entities.User
        {
            UserName = registrationDto.Email,
            Email = registrationDto.Email,
            IsActive = true,
            DisplayName = registrationDto.DisplayName
        };

        var result = await userManager.CreateAsync(user, registrationDto.Password);

        if (!result.Succeeded)
        {
            logger.LogInformation("user {Email} registration failed: {Errors}", registrationDto.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
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
        }

        logger.LogInformation("user {Email} successfully registered.", registrationDto.Email);

        return StatusCode(StatusCodes.Status201Created, new { user.Id, user.Email, user.DisplayName });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user == null)
        {
            logger.LogInformation("user {Email} attempted to log in but the account does not exist.", request.Email);
            return InvalidCredentials(request.Email);
        }

        if (!user.IsActive)
        {
            logger.LogInformation("user {Email} attempted to log in but the account is deactivated.", request.Email);
            return InvalidCredentials(request.Email);
        }

        var validPassword = await userManager.CheckPasswordAsync(user, request.Password);

        if (!validPassword)
            return InvalidCredentials(request.Email);

        var tokens = await GenerateTokensAsync(user);

        logger.LogInformation("user {Email} successfully logged in.", request.Email);

        return Ok(tokens);
    }

    [Authorize]
    [HttpPost("deactivate")]
    public async Task<IActionResult> DeactivateAccount(DeactivateUserRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user == null)
            return UserNotFound(request.Email);

        user.IsActive = false;
        await appDbContext.SaveChangesAsync();

        logger.LogInformation("account {Email} successfully deactivated.", request.Email);

        return Ok();
    }

    [Authorize]
    [HttpPost("reactivate")]
    public async Task<IActionResult> ReactivateAccount(ReactivateUserRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user == null)
            return UserNotFound(request.Email);

        user.IsActive = true;
        await appDbContext.SaveChangesAsync();

        logger.LogInformation("account {Email} successfully reactivated.", request.Email);

        return Ok();
    }
}
