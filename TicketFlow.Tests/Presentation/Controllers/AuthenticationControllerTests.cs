using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using TicketFlow.Application.Authentication.Interfaces;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Contracts.DTOs;
using TicketFlow.Presentation.Controllers;

namespace TicketFlow.Tests.Presentation.Controllers;

public class AuthenticationControllerTests
{
    private readonly Mock<IAuthenticationService> _authService;
    private readonly AuthenticationController _controller;

    public AuthenticationControllerTests()
    {
        _authService = new Mock<IAuthenticationService>();
        _controller = new AuthenticationController(_authService.Object);
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

        _authService
            .Setup(x => x.RefreshTokenAsync(request, CancellationToken.None))
            .ReturnsAsync(new RefreshTokenSucceeded(tokenResponse));

        var result = await _controller.Refresh(request, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RefreshTokenSucceeded>(okResult.Value);

        Assert.Equal(tokenResponse.AccessToken, response.Tokens.AccessToken);
        Assert.Equal(tokenResponse.RefreshToken, response.Tokens.RefreshToken);
        Assert.Equal(tokenResponse.ExpiresAt, response.Tokens.ExpiresAt);
        Assert.Equal(tokenResponse.TokenType, response.Tokens.TokenType);

        _authService.Verify(x => x.RefreshTokenAsync(request, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Refresh_WhenTokenIsInvalid_ReturnsUnauthorized()
    {
        var request = new RefreshTokenRequest
        {
            RefreshToken = "invalid-refresh-token"
        };

        _authService
            .Setup(x => x.RefreshTokenAsync(request, CancellationToken.None))
            .ReturnsAsync(new RefreshTokenInvalid());

        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } };

        var result = await _controller.Refresh(request, CancellationToken.None);
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);

        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);

        Assert.Equal(StatusCodes.Status401Unauthorized, problemDetails.Status);
        Assert.Equal("Invalid refresh token.", problemDetails.Title);
        Assert.Equal("The refresh token is invalid or expired.", problemDetails.Detail);

        _authService.Verify(x => x.RefreshTokenAsync(request, CancellationToken.None), Times.Once);
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

        _authService
            .Setup(x => x.RefreshTokenAsync(request, cancellationToken))
            .ReturnsAsync(new RefreshTokenInvalid());

        await _controller.Refresh(request, cancellationToken);

        _authService.Verify(x => x.RefreshTokenAsync(request, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Register_WhenSuccessful_ReturnsCreated()
    {
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "Password123!"
        };

        _authService
            .Setup(x => x.RegisterAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistrationSucceeded(new Domain.Entities.User { Email = request.Email }));

        var result = await _controller.Register(request, CancellationToken.None);

        var createdResponse = Assert.IsType<ObjectResult>(result);
        var createdUser = Assert.IsType<RegisterUserResponseDto>(createdResponse.Value);

        Assert.Equal(StatusCodes.Status201Created, createdResponse.StatusCode);
        Assert.Equal(request.Email, createdUser.Email);

        _authService.Verify(x => x.RegisterAsync(request, CancellationToken.None), Times.Once);
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

        _authService
            .Setup(x => x.RegisterAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistrationFailed(errors));

        var result = await _controller.Register(request,CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);

        var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);

        Assert.Contains("Email", problem.Errors.Keys);
        Assert.Contains(
            "Email is already registered.",
            problem.Errors["Email"]);

        _authService.Verify(x => x.RegisterAsync(request, CancellationToken.None),  Times.Once);
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

        _authService
            .Setup(x => x.LoginAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginSucceeded(tokens));

        var result = await _controller.Login(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);

        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);

        var response = Assert.IsType<TokenResponse>(ok.Value);

        Assert.Equal(tokens.AccessToken, response.AccessToken);
        Assert.Equal(tokens.RefreshToken, response.RefreshToken);
        Assert.Equal(tokens.ExpiresAt, response.ExpiresAt);
        Assert.Equal(tokens.TokenType, response.TokenType);

        _authService.Verify(
            x => x.LoginAsync(request, CancellationToken.None),
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

        _authService
            .Setup(x => x.LoginAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvalidCredentials());

        var result = await _controller.Login(request, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);

        var problem = Assert.IsType<ProblemDetails>(unauthorized.Value);

        Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
        Assert.Equal("Invalid credentials.", problem.Title);

        _authService.Verify(x => x.LoginAsync(request, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Deactivate_WhenSuccessful_ReturnsSuccessfulOk()
    {
        SetAuthenticatedUser("some-authenticated-user");
        var email = "test@email.com";
        var deactivateRequest = new DeactivateUserRequest { Email = email };

        _authService
            .Setup(x => x.DeactivateAccountAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountDeactivated());

        var result = await _controller.DeactivateAccount(deactivateRequest, CancellationToken.None);
        var noContent = Assert.IsType<OkResult>(result);

        Assert.Equal(StatusCodes.Status200OK, noContent.StatusCode);
        _authService.Verify(x => x.DeactivateAccountAsync(email, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Deactivate_WhenUserNotFound_ReturnsNotFound()
    {
        SetAuthenticatedUser("some-user-id");

        var testEmail = "test@user.com";

        _authService
            .Setup(x => x.DeactivateAccountAsync(testEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountNotFound());

        var result = await _controller.DeactivateAccount(new DeactivateUserRequest { Email = testEmail }, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);

        _authService.Verify(x => x.DeactivateAccountAsync(testEmail, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Reactivate_WhenSuccessful_ReturnsSuccessfulOk()
    {
        SetAuthenticatedUser("some-authenticated-user");
        var email = "test@email.com";
        var reactivateUserRequest = new ReactivateUserRequest { Email = email };

        _authService
            .Setup(x => x.ReactivateAccountAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountReactivated());

        var result = await _controller.ReactivateAccount(reactivateUserRequest, CancellationToken.None);
        var noContent = Assert.IsType<OkResult>(result);

        Assert.Equal(StatusCodes.Status200OK, noContent.StatusCode);
        _authService.Verify(x => x.ReactivateAccountAsync(email, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ReActivate_WhenUserNotFound_ReturnsNotFound()
    {
        SetAuthenticatedUser("some-user-id");

        var testEmail = "test@user.com";

        _authService
            .Setup(x => x.ReactivateAccountAsync(testEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountNotFound());

        var result = await _controller.ReactivateAccount(new ReactivateUserRequest { Email = testEmail }, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);

        _authService.Verify(x => x.ReactivateAccountAsync(testEmail, CancellationToken.None), Times.Once);
    }

    private void SetAuthenticatedUser(string userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, "TestAuthentication");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
    }
}
