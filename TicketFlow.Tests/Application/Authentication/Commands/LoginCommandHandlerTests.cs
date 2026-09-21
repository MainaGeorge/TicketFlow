using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Authentication.Commands.Login;
using TicketFlow.Application.Authentication.Interfaces;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Contracts.Authentication;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Authentication.Commands;

public class LoginCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identityService;
    private readonly Mock<ITokenService> _tokenService;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepository;
    private readonly Mock<ILogger<LoginCommandHandler>> _logger;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _identityService = new Mock<IIdentityService>();
        _tokenService = new Mock<ITokenService>();
        _refreshTokenRepository = new Mock<IRefreshTokenRepository>();
        _logger = new Mock<ILogger<LoginCommandHandler>>();
        _handler = new LoginCommandHandler(_identityService.Object, _tokenService.Object, _refreshTokenRepository.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_WhenCalledWithValidCredentials_ReturnsLoginSucceeded()
    {
        var (email, password) = ("test@email.com", "password");
        var command = new LoginCommand(email, password);
        var activeUser = new User { Id = "user-id", Email = email };
        var expiresAt = DateTime.UtcNow;
        var tokenResponse = new TokenResponse { AccessToken = "access token", ExpiresAt = expiresAt, RefreshToken = "refresh token", TokenType = "bearer" };

        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeUser);

        _identityService
            .Setup(x => x.CheckPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _tokenService
            .Setup(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenResponse);

        _refreshTokenRepository
            .Setup(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepository
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var loginResult = await _handler.Handle(command, CancellationToken.None);
        var loginSucceed = Assert.IsType<LoginSucceeded>(loginResult);

        Assert.Equal(tokenResponse.AccessToken, loginSucceed.Tokens.AccessToken);
        Assert.Equal(tokenResponse.ExpiresAt, loginSucceed.Tokens.ExpiresAt);
        Assert.Equal(tokenResponse.TokenType, loginSucceed.Tokens.TokenType);
        Assert.Equal(tokenResponse.RefreshToken, loginSucceed.Tokens.RefreshToken);

        _identityService.Verify(x => x.FindByEmailAsync(email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.CheckPasswordAsync(activeUser, password, CancellationToken.None), Times.Once);
        _tokenService.Verify(x => x.GenerateTokensAsync(activeUser, CancellationToken.None), Times.Once);
        _refreshTokenRepository.Verify(
            x => x.AddAsync(
                It.Is<RefreshToken>(r => r.UserId == "user-id" && r.Token == tokenResponse.RefreshToken && r.UserId == activeUser.Id && r.ExpiresAt > DateTimeOffset.UtcNow),
                It.IsAny<CancellationToken>()), Times.Once);

        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserDoesnotExist_ReturnsInvalidCredentials()
    {
        var (email, password) = ("test@email.com", "password");
        var command = new LoginCommand(email, password);

        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var loginResult = await _handler.Handle(command, CancellationToken.None);

        Assert.IsType<InvalidCredentials>(loginResult);

        _identityService.Verify(x => x.FindByEmailAsync(email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.CheckPasswordAsync(It.Is<User>(e => e.Email == email), password, CancellationToken.None), Times.Never);
        _tokenService.Verify(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserIsInActive_ReturnsInvalidCredentials()
    {
        var (email, password) = ("test@email.com", "password");
        var command = new LoginCommand(email, password);
        var inactiveUser = new User { Email = email };
        inactiveUser.Deactivate();

        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveUser);

        var loginResult = await _handler.Handle(command, CancellationToken.None);

        Assert.IsType<InvalidCredentials>(loginResult);

        _identityService.Verify(x => x.FindByEmailAsync(email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.CheckPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokenService.Verify(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPasswordIsInvalid_ReturnsInvalidCredentials()
    {
        var (email, password) = ("test@email.com", "password");
        var command = new LoginCommand(email, password);
        var activeUser = new User { Email = email };

        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeUser);

        _identityService
            .Setup(x => x.CheckPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync(false);

        var loginResult = await _handler.Handle(command, CancellationToken.None);

        Assert.IsType<InvalidCredentials>(loginResult);

        _identityService.Verify(x => x.FindByEmailAsync(email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.CheckPasswordAsync(activeUser, password, CancellationToken.None), Times.Once);
        _tokenService.Verify(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenIdentityThrows_PropagatesException()
    {
        var exception = new InvalidOperationException("Unexpected error");
        var (email, password) = ("test@email.com", "password");
        var command = new LoginCommand(email, password);

        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));

        Assert.Same(exception, result);

        _identityService.Verify(x => x.FindByEmailAsync(email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.CheckPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokenService.Verify(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTokenServiceThrows_PropagatesException()
    {
        var exception = new InvalidOperationException("Unexpected error");
        var (email, password) = ("test@email.com", "password");
        var command = new LoginCommand(email, password);
        var activeUser = new User { Email = email };

        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeUser);

        _identityService
            .Setup(x => x.CheckPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync(true);

        _tokenService
            .Setup(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));

        Assert.Same(exception, result);

        _identityService.Verify(x => x.FindByEmailAsync(email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.CheckPasswordAsync(It.Is<User>(u => u.Email == email), password, CancellationToken.None), Times.Once);
        _tokenService.Verify(x => x.GenerateTokensAsync(It.Is<User>(u => u.Email == email), CancellationToken.None), Times.Once);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_PropagatesException()
    {
        var exception = new InvalidOperationException("Unexpected error");
        var (email, password) = ("test@email.com", "password");
        var command = new LoginCommand(email, password);
        var activeUser = new User { Email = email };
        var tokenResponse = new TokenResponse { AccessToken = "access token", ExpiresAt = DateTime.UtcNow, RefreshToken = "refresh token", TokenType = "bearer" };

        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeUser);

        _identityService
            .Setup(x => x.CheckPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync(true);

        _tokenService
            .Setup(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenResponse);

        _refreshTokenRepository
            .Setup(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));

        Assert.Same(exception, result);

        _identityService.Verify(x => x.FindByEmailAsync(email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.CheckPasswordAsync(It.Is<User>(u => u.Email == email), password, CancellationToken.None), Times.Once);
        _tokenService.Verify(x => x.GenerateTokensAsync(It.Is<User>(u => u.Email == email), CancellationToken.None), Times.Once);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.AddAsync(
            It.Is<RefreshToken>(r => r.Token == tokenResponse.RefreshToken && r.UserId == activeUser.Id && r.ExpiresAt > DateTimeOffset.UtcNow),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesThrows_PropagatesException()
    {
        var exception = new InvalidOperationException("Unexpected error");
        var (email, password) = ("test@email.com", "password");

        var command = new LoginCommand(email, password);

        var activeUser = new User
        {
            Id = "user-id",
            Email = email,
        };

        var tokenResponse = new TokenResponse
        {
            AccessToken = "access token",
            ExpiresAt = DateTime.UtcNow,
            RefreshToken = "refresh token",
            TokenType = "bearer"
        };

        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeUser);

        _identityService
            .Setup(x => x.CheckPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _tokenService
            .Setup(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenResponse);

        _refreshTokenRepository
            .Setup(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepository
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));

        Assert.Same(exception, error);

        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(),CancellationToken.None), Times.Once);

        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(CancellationToken.None), Times.Once);
    }
}
