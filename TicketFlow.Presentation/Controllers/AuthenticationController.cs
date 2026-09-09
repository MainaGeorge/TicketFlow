using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketFlow.Application.Authentication.Interfaces;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Contracts.DTOs;

namespace TicketFlow.Presentation.Controllers;

[Route("api/auth")]
[ApiController]
[ApiVersion("1.0")]
public class AuthenticationController(IAuthenticationService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest registrationDto, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(registrationDto, cancellationToken);

        return result switch
        {
            RegistrationFailed failedRegistration => BadRequest(new ValidationProblemDetails(
                failedRegistration
                .Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()))
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "User Registration Failed.",
                    Instance = HttpContext.Request.Path
                 }),

            RegistrationSucceeded successfulRegistration =>
                StatusCode(StatusCodes.Status201Created, 
                new 
                { 
                    successfulRegistration.User.Id, 
                    successfulRegistration.User.Email,
                    successfulRegistration.User.DisplayName 
                }),

            _ => throw new InvalidOperationException("unknown registration result")
        };
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var loginResult = await authService.LoginAsync(request, cancellationToken);
        return loginResult switch
        {
            InvalidCredentials _ => Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid credentials.",
                Detail = "Email or password is incorrect.",
                Instance = HttpContext.Request.Path
            }),
            LoginSucceeded successfulLogin => Ok(successfulLogin.Tokens),
            _ => throw new InvalidOperationException("unknown login result")
        };
    }

    [Authorize]
    [HttpPost("deactivate")]
    public async Task<IActionResult> DeactivateAccount(DeactivateUserRequest request, CancellationToken cancellationToken)
    {
        var deActivationResult = await authService.DeactivateAccountAsync(request.Email, cancellationToken);

        return deActivationResult switch
        {
            AccountDeactivated _ => Ok(),
            AccountAlreadyDeactivated => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "User already deactivated.",
                Detail = "The specified user is already deactivated.",
                Instance = HttpContext.Request.Path
            }),
            AccountNotFound _ => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User not found.",
                Detail = "The specified user could not be found.",
                Instance = HttpContext.Request.Path
            }),
            AccountDeActivationFailed _ => StatusCode(StatusCodes.Status500InternalServerError),
            _ => throw new InvalidOperationException("unknown deactivation result")
        };
    }

    [Authorize]
    [HttpPost("reactivate")]
    public async Task<IActionResult> ReactivateAccount(ReactivateUserRequest request, CancellationToken cancellationToken)
    {
        var activationResult = await authService.ReactivateAccountAsync(request.Email, cancellationToken);

        return activationResult switch
        {
            AccountReactivated _ => Ok(),
            AccountAlreadyActivated => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "User already activated.",
                Detail = "The specified user is already active.",
                Instance = HttpContext.Request.Path
            }),
            AccountNotFound _ => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User not found.",
                Detail = "The specified user could not be found.",
                Instance = HttpContext.Request.Path
            }),
            _ => throw new InvalidOperationException("unknown deactivation result")
        };
    }
}
