using Moq;
using TicketFlow.Application.Abstractions.Authentication;
using TicketFlow.Application.Abstractions.Repositories;
using TicketFlow.Application.Authentication.Commands.RefreshToken;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Contracts.Authentication;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Authentication.Commands;

public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<ITokenService> _tokenService;
    private readonly Mock<IIdentityService> _identityService;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepository;
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _tokenService = new Mock<ITokenService>();
        _refreshTokenRepository = new Mock<IRefreshTokenRepository>();
        _identityService = new Mock<IIdentityService>();
        _handler = new RefreshTokenCommandHandler(_refreshTokenRepository.Object, _identityService.Object, _tokenService.Object);
    }

    [Fact]
    public async Task Handle_WhenTokenValid_ReturnsRefreshTokenSucceeded()
    {
        var oldRefreshToken = "some-existing-token";
        var newRefreshToken = "some-new-refresh-token";
        var accessToken = "some-new -access-token";
        var expiresAt = DateTime.UtcNow.AddDays(2);
        var userId = Guid.NewGuid().ToString();
        var user = new User { Id = userId };
        var refreshToken = new RefreshToken { Token = oldRefreshToken, User = user, UserId = userId, ExpiresAt = DateTime.UtcNow.AddDays(10) };

        var tokenResponse = new TokenResponse { AccessToken = accessToken, ExpiresAt = expiresAt, TokenType = "Bearer", RefreshToken = newRefreshToken };

        _refreshTokenRepository
            .Setup(x => x.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshToken);

        _refreshTokenRepository
            .Setup(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepository
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _tokenService
            .Setup(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenResponse);

        var refreshTokenCommand = new RefreshTokenCommand(oldRefreshToken);
        var result = await _handler.Handle(refreshTokenCommand, CancellationToken.None);
        var refreshTokenResult = Assert.IsType<RefreshTokenSucceeded>(result);

        _refreshTokenRepository.Verify(x => x.AddAsync(It.Is<RefreshToken>(token => token.Token == refreshTokenResult.Tokens.RefreshToken && token.UserId == refreshToken.UserId), CancellationToken.None), Times.Once);
        _refreshTokenRepository.Verify(x => x.GetByTokenAsync(oldRefreshToken, CancellationToken.None), Times.Once);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _tokenService.Verify(x => x.GenerateTokensAsync(user, It.IsAny<IEnumerable<string>>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_PassesCancellationToken_ToRepositoryAndService()
    {
        var oldRefreshToken = "some-existing-token";
        var newRefreshToken = "some-new-refresh-token";
        var accessToken = "some-new -access-token";
        var expiresAt = DateTime.UtcNow.AddDays(2);
        var userId = Guid.NewGuid().ToString();
        var user = new User { Id = userId };
        var refreshToken = new RefreshToken { Token = oldRefreshToken, User = user, ExpiresAt = DateTime.UtcNow.AddDays(10) };
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        var tokenResponse = new TokenResponse { AccessToken = accessToken, ExpiresAt = expiresAt, TokenType = "Bearer", RefreshToken = newRefreshToken };

        _refreshTokenRepository
            .Setup(x => x.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshToken);

        _refreshTokenRepository
            .Setup(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepository
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _tokenService
            .Setup(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenResponse);

        var refreshTokenCommand = new RefreshTokenCommand(oldRefreshToken);
        var result = await _handler.Handle(refreshTokenCommand, token);

        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), token), Times.Once);
        _refreshTokenRepository.Verify(x => x.GetByTokenAsync(oldRefreshToken, token), Times.Once);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), token), Times.Once);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(token), Times.Once);
        _tokenService.Verify(x => x.GenerateTokensAsync(user, It.IsAny<IEnumerable<string>>(), token), Times.Once);
    }

    [Fact]
    public async Task RefreshToken_WhenValid_RotatesTokenAndReturnsNewTokens()
    {
        var email = "test@email.com";
        var oldRefreshToken = new RefreshToken
        {
            Id = 1,
            Token = "old-refresh-token",
            UserId = "user-1",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(6),
            User = new User
            {
                Id = "user-1",
                Email = email,
                UserName = email,
            }
        };

        var newTokenResponse = new TokenResponse
        {
            AccessToken = "new-access-token",
            RefreshToken = "new-refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            TokenType = "Bearer"
        };

        _refreshTokenRepository
            .Setup(x => x.GetByTokenAsync(oldRefreshToken.Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRefreshToken);

        _tokenService
            .Setup(x => x.GenerateTokensAsync(oldRefreshToken.User, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newTokenResponse);

        RefreshToken? replacementToken = null;

        _refreshTokenRepository
            .Setup(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((token, _) => replacementToken = token)
            .Returns(Task.CompletedTask);

        _refreshTokenRepository
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new RefreshTokenCommand(oldRefreshToken.Token);
        var result = await _handler.Handle(command, CancellationToken.None);
        var success = Assert.IsType<RefreshTokenSucceeded>(result);

        Assert.Equal(newTokenResponse.AccessToken, success.Tokens.AccessToken);
        Assert.Equal(newTokenResponse.RefreshToken, success.Tokens.RefreshToken);
        Assert.NotNull(oldRefreshToken.RevokedAt);
        Assert.Equal(newTokenResponse.RefreshToken, oldRefreshToken.ReplacedBy);
        Assert.NotNull(replacementToken);
        Assert.Equal(newTokenResponse.RefreshToken, replacementToken.Token);
        Assert.Equal(oldRefreshToken.UserId, replacementToken.UserId);

        _refreshTokenRepository.Verify(x => x.GetByTokenAsync(oldRefreshToken.Token, CancellationToken.None), Times.Once);
        _tokenService.Verify(x => x.GenerateTokensAsync(oldRefreshToken.User, It.IsAny<IEnumerable<string>>(), CancellationToken.None), Times.Once);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(CancellationToken.None), Times.Once);

        _refreshTokenRepository.Verify(
            x => x.AddAsync(It.Is<RefreshToken>(r =>
                    r.Token == newTokenResponse.RefreshToken &&
                    r.UserId == oldRefreshToken.UserId),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTokenIsRevoked_ReturnsInvalidAndDoesNotRotateAgain()
    {
        var email = "test@user.com";
        var oldRefreshToken = new RefreshToken
        {
            Id = 1,
            Token = "old-refresh-token",
            UserId = "user-1",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(6),
            RevokedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ReplacedBy = "new-refresh-token",
            User = new User
            {
                Id = "user-1",
                Email = email,
                UserName = email,
            }
        };

        _refreshTokenRepository.Setup(x => x.GetByTokenAsync(oldRefreshToken.Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRefreshToken);

        var command = new RefreshTokenCommand(oldRefreshToken.Token);
        var result = await _handler.Handle(command, CancellationToken.None);
       
        Assert.IsType<RefreshTokenInvalid>(result);

        _tokenService.Verify(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTokenIsExpired_ReturnsInvalidAndDoesNotRotateAgain()
    {
        var email = "test@user.com";
        var oldRefreshToken = new RefreshToken
        {
            Id = 1,
            Token = "old-refresh-token",
            UserId = "user-1",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
            User = new User
            {
                Id = "user-1",
                Email = email,
                UserName = email,
            }
        };

        _refreshTokenRepository.Setup(x => x.GetByTokenAsync(oldRefreshToken.Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRefreshToken);

        var command = new RefreshTokenCommand(oldRefreshToken.Token);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.IsType<RefreshTokenInvalid>(result);

        _tokenService.Verify(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTokenIsNull_ReturnsInvalidAndDoesNotRotateAgain()
    {
        var token = "someRefreshToken";

        _refreshTokenRepository.Setup(x => x.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?) null);

        var command = new RefreshTokenCommand(token);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.IsType<RefreshTokenInvalid>(result);

        _tokenService.Verify(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserIsNull_ReturnsInvalidAndDoesNotRotateAgain()
    {
        var oldRefreshToken = new RefreshToken
        {
            Id = 1,
            Token = "old-refresh-token",
            UserId = null!,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(6),
            ReplacedBy = null,
            RevokedAt = null,
            User = null!
        };

        _refreshTokenRepository
            .Setup(x => x.GetByTokenAsync(oldRefreshToken.Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRefreshToken);

        var command = new RefreshTokenCommand(oldRefreshToken.Token);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.IsType<RefreshTokenInvalid>(result);

        _tokenService.Verify(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserIsInactive_ReturnsInvalidAndDoesNotRotateAgain()
    {
        var email = "test@user.com";
        var oldRefreshToken = new RefreshToken
        {
            Id = 1,
            Token = "old-refresh-token",
            UserId = "user-1",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(6),
            RevokedAt = null,
            ReplacedBy = null,
            User = new User
            {
                Id = "user-1",
                Email = email,
                UserName = email
            }
        };

        oldRefreshToken.User.Deactivate();

        _refreshTokenRepository
            .Setup(x => x.GetByTokenAsync(oldRefreshToken.Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRefreshToken);

        var command = new RefreshTokenCommand(oldRefreshToken.Token);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.IsType<RefreshTokenInvalid>(result);

        _tokenService.Verify(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesAsyncThrows_PropagatesException()
    {
        var oldRefreshToken = "some-existing-token";
        var newRefreshToken = "some-new-refresh-token";
        var accessToken = "some-new -access-token";
        var expiresAt = DateTime.UtcNow.AddDays(2);
        var userId = Guid.NewGuid().ToString();
        var user = new User { Id = userId };
        var refreshToken = new RefreshToken { Token = oldRefreshToken, User = user, ExpiresAt = DateTime.UtcNow.AddDays(10) };
        var exception = new InvalidOperationException("Something terrible");

        var tokenResponse = new TokenResponse { AccessToken = accessToken, ExpiresAt = expiresAt, TokenType = "Bearer", RefreshToken = newRefreshToken };

        _refreshTokenRepository
            .Setup(x => x.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshToken);

        _refreshTokenRepository
            .Setup(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepository
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        _tokenService
            .Setup(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenResponse);

        var refreshTokenCommand = new RefreshTokenCommand(oldRefreshToken);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(refreshTokenCommand, CancellationToken.None));
        Assert.Same(exception, error);

        _refreshTokenRepository.Verify(x => x.AddAsync(It.Is<RefreshToken>(token => token.UserId == refreshToken.UserId), CancellationToken.None), Times.Once);
        _refreshTokenRepository.Verify(x => x.GetByTokenAsync(oldRefreshToken, CancellationToken.None), Times.Once);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _tokenService.Verify(x => x.GenerateTokensAsync(user, It.IsAny<IEnumerable<string>>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAddAsyncThrows_PropagatesException()
    {
        var oldRefreshToken = "some-existing-token";
        var newRefreshToken = "some-new-refresh-token";
        var accessToken = "some-new -access-token";
        var expiresAt = DateTime.UtcNow.AddDays(2);
        var userId = Guid.NewGuid().ToString();
        var user = new User { Id = userId };
        var refreshToken = new RefreshToken { Token = oldRefreshToken, User = user, ExpiresAt = DateTime.UtcNow.AddDays(10) };
        var exception = new InvalidOperationException("Something terrible");

        var tokenResponse = new TokenResponse { AccessToken = accessToken, ExpiresAt = expiresAt, TokenType = "Bearer", RefreshToken = newRefreshToken };

        _refreshTokenRepository
            .Setup(x => x.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshToken);

        _refreshTokenRepository
            .Setup(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        _tokenService
            .Setup(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenResponse);

        var refreshTokenCommand = new RefreshTokenCommand(oldRefreshToken);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(refreshTokenCommand, CancellationToken.None));
        Assert.Same(exception, error);

        _refreshTokenRepository.Verify(x => x.AddAsync(It.Is<RefreshToken>(token => token.UserId == refreshToken.UserId), CancellationToken.None), Times.Once);
        _refreshTokenRepository.Verify(x => x.GetByTokenAsync(oldRefreshToken, CancellationToken.None), Times.Once);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _tokenService.Verify(x => x.GenerateTokensAsync(user, It.IsAny<IEnumerable<string>>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenGenerateTokensThrows_PropagatesException()
    {
        var oldRefreshToken = "some-existing-token";
        var newRefreshToken = "some-new-refresh-token";
        var accessToken = "some-new -access-token";
        var expiresAt = DateTime.UtcNow.AddDays(2);
        var userId = Guid.NewGuid().ToString();
        var user = new User { Id = userId };
        var refreshToken = new RefreshToken { Token = oldRefreshToken, User = user, ExpiresAt = DateTime.UtcNow.AddDays(10) };
        var exception = new InvalidOperationException("Something terrible");

        var tokenResponse = new TokenResponse { AccessToken = accessToken, ExpiresAt = expiresAt, TokenType = "Bearer", RefreshToken = newRefreshToken };

        _refreshTokenRepository
            .Setup(x => x.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshToken);

        _tokenService
            .Setup(x => x.GenerateTokensAsync(It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var refreshTokenCommand = new RefreshTokenCommand(oldRefreshToken);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(refreshTokenCommand, CancellationToken.None));
        Assert.Same(exception, error);

        _refreshTokenRepository.Verify(x => x.AddAsync(It.Is<RefreshToken>(token => token.UserId == refreshToken.UserId), CancellationToken.None), Times.Never);
        _refreshTokenRepository.Verify(x => x.GetByTokenAsync(oldRefreshToken, CancellationToken.None), Times.Once);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _tokenService.Verify(x => x.GenerateTokensAsync(user, It.IsAny<IEnumerable<string>>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenGetByTokenAsyncThrows_PropagatesException()
    {
        var oldRefreshToken = "some-existing-token";
        var newRefreshToken = "some-new-refresh-token";
        var accessToken = "some-new -access-token";
        var expiresAt = DateTime.UtcNow.AddDays(2);
        var userId = Guid.NewGuid().ToString();
        var user = new User { Id = userId };
        var refreshToken = new RefreshToken { Token = oldRefreshToken, User = user, ExpiresAt = DateTime.UtcNow.AddDays(10) };
        var exception = new InvalidOperationException("Something terrible");

        var tokenResponse = new TokenResponse { AccessToken = accessToken, ExpiresAt = expiresAt, TokenType = "Bearer", RefreshToken = newRefreshToken };

        _refreshTokenRepository
            .Setup(x => x.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var refreshTokenCommand = new RefreshTokenCommand(oldRefreshToken);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(refreshTokenCommand, CancellationToken.None));
        Assert.Same(exception, error);

        _refreshTokenRepository.Verify(x => x.AddAsync(It.Is<RefreshToken>(token => token.UserId == refreshToken.UserId), CancellationToken.None), Times.Never);
        _refreshTokenRepository.Verify(x => x.GetByTokenAsync(oldRefreshToken, CancellationToken.None), Times.Once);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _tokenService.Verify(x => x.GenerateTokensAsync(user, It.IsAny<IEnumerable<string>>(), CancellationToken.None), Times.Never);
    }
}
