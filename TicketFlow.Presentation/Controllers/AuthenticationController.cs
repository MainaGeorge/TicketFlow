using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketFlow.Application.Authentication.Commands.DeactivateUser;
using TicketFlow.Application.Authentication.Commands.Login;
using TicketFlow.Application.Authentication.Commands.ReactivateUser;
using TicketFlow.Application.Authentication.Commands.RefreshToken;
using TicketFlow.Application.Authentication.Commands.Register;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Contracts.Authentication;
using TicketFlow.Presentation.Mappings;

namespace TicketFlow.Presentation.Controllers;

[Route("api/auth")]
[ApiController]
[ApiVersion("1.0")]
[Authorize]
public class AuthenticationController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest registrationDto, CancellationToken cancellationToken)
    {
        var command = new RegisterCommand(registrationDto.Email, registrationDto.Password, registrationDto.DisplayName);
        var result = await sender.Send(command, cancellationToken);

        return result switch
        {
            EmailAlreadyRegistered duplicateEmail => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Email already registered.",
                Detail = $"An account with the email address '{registrationDto.Email}' already exists.",
                Instance = HttpContext.Request.Path
            }),
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
                successfulRegistration.User.MapToRegisterUserDto()),
            _ => throw new InvalidOperationException("unknown registration result")
        };
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var loginResult = await sender.Send(command, cancellationToken);
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

    [HttpPost("deactivate")]
    public async Task<IActionResult> DeactivateAccount(DeactivateUserRequest request, CancellationToken cancellationToken)
    {
        var command = new DeactivateUserCommand(request.Email);
        var deActivationResult = await sender.Send(command, cancellationToken);

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

    [HttpPost("reactivate")]
    public async Task<IActionResult> ReactivateAccount(ReactivateUserRequest request, CancellationToken cancellationToken)
    {
        var command = new ReactivateUserCommand(request.Email);
        var activationResult = await sender.Send(command, cancellationToken);

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
            AccountActivationFailed => StatusCode(StatusCodes.Status500InternalServerError),
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

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var command = new RefreshTokenCommand(request.RefreshToken);
        var result = await sender.Send(command, cancellationToken);

        return result switch
        {
            RefreshTokenSucceeded success => Ok(success),
            RefreshTokenInvalid => Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid refresh token.",
                Detail = "The refresh token is invalid or expired.",
                Instance = HttpContext.Request.Path
            }),
            _ => throw new InvalidOperationException("Unknown refresh token result.")
        };
    }
}
