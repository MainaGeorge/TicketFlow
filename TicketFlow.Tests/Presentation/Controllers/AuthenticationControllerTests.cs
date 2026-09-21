using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using TicketFlow.Application.Authentication.Commands.DeactivateUser;
using TicketFlow.Application.Authentication.Commands.Login;
using TicketFlow.Application.Authentication.Commands.ReactivateUser;
using TicketFlow.Application.Authentication.Commands.RefreshToken;
using TicketFlow.Application.Authentication.Commands.Register;
using TicketFlow.Application.Authentication.Interfaces;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Contracts.Authentication;
using TicketFlow.Domain.Entities;
using TicketFlow.Presentation.Controllers;

namespace TicketFlow.Tests.Presentation.Controllers;

public class AuthenticationControllerTests
{
    private readonly Mock<ISender> _sender;
    private readonly AuthenticationController _controller;

    public AuthenticationControllerTests()
    {
        _sender = new Mock<ISender>();
        _controller = new AuthenticationController(_sender.Object);
    }

    [Fact]
    public async Task Refresh_WhenTokenIsValid_ReturnsOkWithTokens()
    {
        var request = new RefreshTokenRequest
        {
            RefreshToken = "old-refresh-token"
        };

        var tokenResponse = new TokenResponse
        {
            AccessToken = "new-access-token",
            RefreshToken = "new-refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            TokenType = "Bearer"
        };

        _sender
            .Setup(x => x.Send(It.IsAny<RefreshTokenCommand>(), CancellationToken.None))
            .ReturnsAsync(new RefreshTokenSucceeded(tokenResponse));

        var result = await _controller.Refresh(request, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RefreshTokenSucceeded>(okResult.Value);

        Assert.Equal(tokenResponse.AccessToken, response.Tokens.AccessToken);
        Assert.Equal(tokenResponse.RefreshToken, response.Tokens.RefreshToken);
        Assert.Equal(tokenResponse.ExpiresAt, response.Tokens.ExpiresAt);
        Assert.Equal(tokenResponse.TokenType, response.Tokens.TokenType);

        _sender.Verify(x => x.Send(It.Is<RefreshTokenCommand>(t => t.Token == request.RefreshToken), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Refresh_WhenTokenIsInvalid_ReturnsUnauthorized()
    {
        var request = new RefreshTokenRequest
        {
            RefreshToken = "invalid-refresh-token"
        };

        _sender
            .Setup(x => x.Send(It.IsAny<RefreshTokenCommand>(), CancellationToken.None))
            .ReturnsAsync(new RefreshTokenInvalid());

        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } };

        var result = await _controller.Refresh(request, CancellationToken.None);
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);

        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);

        Assert.Equal(StatusCodes.Status401Unauthorized, problemDetails.Status);
        Assert.Equal("Invalid refresh token.", problemDetails.Title);
        Assert.Equal("The refresh token is invalid or expired.", problemDetails.Detail);

        _sender.Verify(x => x.Send(It.Is<RefreshTokenCommand>(t => t.Token == request.RefreshToken), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Refresh_PassesCancellationTokenToAuthenticationService()
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var request = new RefreshTokenRequest
        {
            RefreshToken = "refresh-token"
        };

        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } };

        _sender
            .Setup(x => x.Send(It.IsAny<RefreshTokenCommand>(), cancellationToken))
            .ReturnsAsync(new RefreshTokenInvalid());

        await _controller.Refresh(request, cancellationToken);

        _sender.Verify(x => x.Send(It.Is<RefreshTokenCommand>(t => t.Token == request.RefreshToken), cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Register_WhenSuccessful_ReturnsCreated()
    {
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "Password123!"
        };

        _sender
            .Setup(x => x.Send(It.IsAny<RegisterCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistrationSucceeded(new User { Email = request.Email }));

        var result = await _controller.Register(request, CancellationToken.None);

        var createdResponse = Assert.IsType<ObjectResult>(result);
        var createdUser = Assert.IsType<RegisterUserResponseDto>(createdResponse.Value);

        Assert.Equal(StatusCodes.Status201Created, createdResponse.StatusCode);
        Assert.Equal(request.Email, createdUser.Email);

        _sender.Verify(x => x.Send(It.Is<RegisterCommand>(c => c.Email == request.Email && c.Password == request.Password), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Register_WhenRegistrationFails_ReturnsBadRequestWithValidationProblem()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } };

        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "Password123!"
        };

        var errors = new[]
        {
            new IdentityError("Email", "Email is already registered.")
        };

        _sender
          .Setup(x => x.Send(It.IsAny<RegisterCommand>(), CancellationToken.None))
          .ReturnsAsync(new RegistrationFailed(errors));

        var result = await _controller.Register(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);

        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);

        Assert.Contains("Email", problem.Errors.Keys);
        Assert.Contains(
            "Email is already registered.",
            problem.Errors["Email"]);

        _sender.Verify(x => x.Send(It.Is<RegisterCommand>(c => c.Email == request.Email && c.Password == request.Password), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Login_WhenSuccessful_ReturnsOkWithTokenResponse()
    {
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "Password123!"
        };

        var tokens = new TokenResponse
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            TokenType = "Bearer"
        };

        _sender
            .Setup(x => x.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginSucceeded(tokens));

        var result = await _controller.Login(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);

        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);

        var response = Assert.IsType<TokenResponse>(ok.Value);

        Assert.Equal(tokens.AccessToken, response.AccessToken);
        Assert.Equal(tokens.RefreshToken, response.RefreshToken);
        Assert.Equal(tokens.ExpiresAt, response.ExpiresAt);
        Assert.Equal(tokens.TokenType, response.TokenType);

        _sender.Verify(
            x => x.Send(It.Is<LoginCommand>(c => c.Email == request.Email && c.Password == request.Password), CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Login_WhenCredentialsAreInvalid_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } };
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "WrongPassword!"
        };

        _sender
            .Setup(x => x.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvalidCredentials());

        var result = await _controller.Login(request, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);

        var problem = Assert.IsType<ProblemDetails>(unauthorized.Value);

        Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
        Assert.Equal("Invalid credentials.", problem.Title);

        _sender.Verify(x => x.Send(It.Is<LoginCommand>(c => c.Email == request.Email && c.Password == request.Password), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Deactivate_WhenSuccessful_ReturnsSuccessfulOk()
    {
        SetAuthenticatedUser("some-authenticated-user");
        var email = "test@email.com";
        var deactivateRequest = new DeactivateUserRequest { Email = email };

        _sender
            .Setup(x => x.Send(It.IsAny<DeactivateUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountDeactivated());

        var result = await _controller.DeactivateAccount(deactivateRequest, CancellationToken.None);
        var noContent = Assert.IsType<OkResult>(result);

        Assert.Equal(StatusCodes.Status200OK, noContent.StatusCode);
        _sender.Verify(x => x.Send(It.Is<DeactivateUserCommand>(c => c.Email == email), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Deactivate_WhenUserNotFound_ReturnsNotFound()
    {
        SetAuthenticatedUser("some-user-id");

        var testEmail = "test@user.com";

        _sender
            .Setup(x => x.Send(It.IsAny<DeactivateUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountNotFound());

        var result = await _controller.DeactivateAccount(new DeactivateUserRequest { Email = testEmail }, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);

        _sender.Verify(x => x.Send(It.Is<DeactivateUserCommand>(c => c.Email == testEmail), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Reactivate_WhenSuccessful_ReturnsSuccessfulOk()
    {
        SetAuthenticatedUser("some-authenticated-user");
        var email = "test@email.com";
        var reactivateUserRequest = new ReactivateUserRequest { Email = email };

        _sender
            .Setup(x => x.Send(It.IsAny<ReactivateUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountReactivated());

        var result = await _controller.ReactivateAccount(reactivateUserRequest, CancellationToken.None);
        var noContent = Assert.IsType<OkResult>(result);

        Assert.Equal(StatusCodes.Status200OK, noContent.StatusCode);
        _sender.Verify(x => x.Send(It.Is<ReactivateUserCommand>(c => c.Email == email), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ReActivate_WhenUserNotFound_ReturnsNotFound()
    {
        SetAuthenticatedUser("some-user-id");

        var testEmail = "test@user.com";

        _sender
            .Setup(x => x.Send(It.IsAny<ReactivateUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountNotFound());

        var result = await _controller.ReactivateAccount(new ReactivateUserRequest { Email = testEmail }, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);

        _sender.Verify(x => x.Send(It.Is<ReactivateUserCommand>(c => c.Email == testEmail), CancellationToken.None), Times.Once);
    }

    private void SetAuthenticatedUser(string userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, "TestAuthentication");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
    }
}
