using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Authentication.Interfaces;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Application.Authentication.Services;
using TicketFlow.Contracts.DTOs;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Services;

public  class AuthenticationServiceTests
{
    private readonly Mock<ILogger<AuthenticationService>> _logger;
    private readonly Mock<ITokenService> _tokenService;
    private readonly Mock<IIdentityService> _identityService;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepository;
    private readonly AuthenticationService _authenticationService;

    private const string Email = "test@email.com";
    private const string Password = "passowrd34#@233";
    private const string UserName = "Nelson Mandela";


    public AuthenticationServiceTests()
    {
        _logger = new Mock<ILogger<AuthenticationService>>();
        _tokenService = new Mock<ITokenService>();
        _identityService = new Mock<IIdentityService>();
        _refreshTokenRepository = new Mock<IRefreshTokenRepository>();
        _authenticationService = new AuthenticationService(_identityService.Object, _tokenService.Object, _refreshTokenRepository.Object, _logger.Object);
    }

    [Fact]
    public async Task RegisterUser_WhenEmailExists_ReturnsAlreadyRegistered()
    {
        var user = new User { Email = Email };

        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Email = Email });

        var registrationResult = await _authenticationService.RegisterAsync(new RegisterRequest { Email = Email, Password = Email }, CancellationToken.None);

        var emailRegisteredResult = Assert.IsType<EmailAlreadyRegistered>(registrationResult);
        _identityService.Verify(x => x.CreateUserAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterUser_WhenIdentityCreatesUser_ReturnsRegistrationSucceeded()
    {
        var user = new User { Email = Email, UserName = UserName, DisplayName = UserName, IsActive = true};
        var identityCreationResult = new IdentityCreationResult(true, user, null);


        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _identityService
            .Setup(x => x.CreateUserAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(identityCreationResult);

        var registrationResult = await _authenticationService.RegisterAsync(new RegisterRequest { Email = user.Email, Password = Password, DisplayName = user.DisplayName }, CancellationToken.None);

        var userRegistrationResult = Assert.IsType<RegistrationSucceeded>(registrationResult);

        Assert.NotNull(userRegistrationResult.User);
        Assert.Equal(user.Email, userRegistrationResult.User.Email);
        Assert.Equal(user.IsActive, userRegistrationResult.User.IsActive);
        Assert.Equal(user.DisplayName, userRegistrationResult.User.DisplayName);

        _identityService.Verify(x => x.FindByEmailAsync(Email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.CreateUserAsync(
            It.Is<User>(u => u.IsActive == user.IsActive && u.Email == user.Email && u.DisplayName == user.DisplayName),
            Password, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task RegisterUser_WhenIdentityFailsToCreatesUser_ReturnsRegistrationFailed()
    {
        var error = new IdentityError("Creation", "Something went wrong while creating the user");
        var errorList = new List<IdentityError>() { error };
        var userCreationErrors = errorList.AsReadOnly();

        var user = new User { Email = Email };
        var identityFailureResult = new IdentityCreationResult(Success: false, User: null, Errors: userCreationErrors);


        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _identityService
            .Setup(x => x.CreateUserAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(identityFailureResult);

        var registrationResult = await _authenticationService.RegisterAsync(new RegisterRequest { Email = Email, Password = Password }, CancellationToken.None);

        var userRegistrationResult = Assert.IsType<RegistrationFailed>(registrationResult);

        Assert.NotEmpty(userRegistrationResult.Errors);
        Assert.Equal(userCreationErrors, userRegistrationResult.Errors);

        _identityService.Verify(x => x.FindByEmailAsync(Email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.CreateUserAsync(It.Is<User>(u => u.Email == Email), Password), Times.Once);
    }

    [Fact]
    public async Task RegisterUser_WhenIdentityThrowsAnError_ErrorPropagates()
    {
        var message = "Something unexpected happened during find by email async execution";
        var exception = new InvalidOperationException(message);
        var registrationRequest = new RegisterRequest { Email = Email, Password = Password };


        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() => _authenticationService.RegisterAsync(registrationRequest, CancellationToken.None));

        Assert.Equal(message, result.Message);
        _identityService.Verify(x => x.FindByEmailAsync(Email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.CreateUserAsync(It.Is<User>(u => u.Email == Email), Password, CancellationToken.None), Times.Never);
    }

    [Fact]
    public async Task Login_WhenUserDoesnotExist_ReturnsInvalidCredentials()
    {
        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        
        var loginResult = await _authenticationService.LoginAsync(new LoginRequest { Email = Email, Password= Password }, CancellationToken.None);

        Assert.IsType<InvalidCredentials>(loginResult);

        _identityService.Verify(x => x.CheckPasswordAsync(It.Is<User>(e => e.Email == Email), Password, CancellationToken.None), Times.Never);
        _tokenService.Verify(x => x.GenerateTokensAsync(It.Is<User>(e => e.Email == Email), CancellationToken.None), Times.Never);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>()), Times.Never);
    }

    [Fact]
    public async Task Login_WhenUserIsInActive_ReturnsInvalidCredentials()
    {
        var inactiveUser = new User { Email = Email, IsActive = false };

        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveUser);

        var loginResult = await _authenticationService.LoginAsync(new LoginRequest { Email = Email, Password = Password }, CancellationToken.None);

        Assert.IsType<InvalidCredentials>(loginResult);

        _identityService.Verify(x => x.CheckPasswordAsync(inactiveUser, Password, CancellationToken.None), Times.Never);
        _tokenService.Verify(x => x.GenerateTokensAsync(inactiveUser, CancellationToken.None), Times.Never);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>()), Times.Never);
    }

    [Fact]
    public async Task Login_WhenPasswordIsInvalid_ReturnsInvalidCredentials()
    {
        var activeUser = new User { Email = Email, IsActive = true };

        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeUser);

        _identityService
            .Setup(x => x.CheckPasswordAsync(activeUser, Password, CancellationToken.None))
            .ReturnsAsync(false);

        var loginResult = await _authenticationService.LoginAsync(new LoginRequest { Email = Email, Password = Password }, CancellationToken.None);

        Assert.IsType<InvalidCredentials>(loginResult);

        _identityService.Verify(x => x.CheckPasswordAsync(activeUser, Password, CancellationToken.None), Times.Once);
        _tokenService.Verify(x => x.GenerateTokensAsync(activeUser, CancellationToken.None), Times.Never);
        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>()), Times.Never);
    }

    [Fact]
    public async Task Login_WhenCalledWithValidCredentials_ReturnsLoginSucceeded()
    {
        var activeUser = new User { Email = Email, IsActive = true };
        var expiresAt = DateTime.UtcNow;
        var tokenResponse = new TokenResponse { AccessToken = "access token", ExpiresAt = expiresAt, RefreshToken = "refresh token", TokenType = "bearer" };

        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeUser);

        _identityService
            .Setup(x => x.CheckPasswordAsync(activeUser, Password, CancellationToken.None))
            .ReturnsAsync(true);

        _tokenService
            .Setup(x => x.GenerateTokensAsync(activeUser, CancellationToken.None))
            .ReturnsAsync(tokenResponse);

        _refreshTokenRepository
            .Setup(x => x.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var loginResult = await _authenticationService.LoginAsync(new LoginRequest { Email = Email, Password = Password }, CancellationToken.None);
        var loginSucceed = Assert.IsType<LoginSucceeded>(loginResult);

        Assert.Equal(tokenResponse.AccessToken, loginSucceed.Tokens.AccessToken);
        Assert.Equal(tokenResponse.ExpiresAt, loginSucceed.Tokens.ExpiresAt);
        Assert.Equal(tokenResponse.TokenType, loginSucceed.Tokens.TokenType);
        Assert.Equal(tokenResponse.RefreshToken, loginSucceed.Tokens.RefreshToken);

        _identityService.Verify(x => x.CheckPasswordAsync(activeUser, Password, CancellationToken.None), Times.Once);
        _tokenService.Verify(x => x.GenerateTokensAsync(activeUser, CancellationToken.None), Times.Once);
        _refreshTokenRepository.Verify( 
            x => x.AddAsync(
                It.Is<RefreshToken>(r => r.Token == tokenResponse.RefreshToken && r.UserId == activeUser.Id && r.ExpiresAt > DateTimeOffset.UtcNow),
                It.IsAny<CancellationToken>()), Times.Once);

        _refreshTokenRepository.Verify(x => x.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Login_WhenIdentityThrows_PropagatesException()
    {
        var exception = new InvalidOperationException("Unexpected error");
        var request = new LoginRequest { Email = Email, Password = Password };

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() => _authenticationService.LoginAsync(request, CancellationToken.None));

        Assert.Equal("Unexpected error", result.Message);

        _identityService.Verify(
            x => x.CheckPasswordAsync(
                It.IsAny<User>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _tokenService.Verify(
            x => x.GenerateTokensAsync(
                It.IsAny<User>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeactivateUser_WhenUserDoesntExist_ReturnsAccountNotFound()
    {
        _identityService
            .Setup(x => x.FindByEmailAsync(Email, CancellationToken.None))
            .ReturnsAsync((User?)null);

        var deactivateResult = await _authenticationService.DeactivateAccountAsync(Email, CancellationToken.None);

        Assert.IsType<AccountNotFound>(deactivateResult);
        
        _identityService.Verify(x => x.UpdateUserAsync(It.IsAny<User>(), CancellationToken.None), Times.Never);
    }

    [Fact]
    public async Task DeactivateUser_WhenUserIsInActive_ReturnsAlreadyDeactivatedResult()
    {
        var inactiveUser = new User { Email = Email, IsActive = false };

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, CancellationToken.None))
            .ReturnsAsync(inactiveUser);

        var deactivateResult = await _authenticationService.DeactivateAccountAsync(Email, CancellationToken.None);

        Assert.IsType<AccountAlreadyDeactivated>(deactivateResult);

        _identityService.Verify(x => x.UpdateUserAsync(inactiveUser, CancellationToken.None), Times.Never);
    }


    [Fact]
    public async Task DeactivateUser_WhenUserUpdateFails_ReturnsAccountDeActivationFailedResult()
    {
        var activeUser = new User { Email = Email, IsActive = true };
        var error = new IdentityError("Creation", "Something went wrong while updating the user");
        var errorList = new List<IdentityError>() { error };
        var userUpdateErrors = errorList.AsReadOnly();

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, CancellationToken.None))
            .ReturnsAsync(activeUser);

        _identityService
            .Setup(x => x.UpdateUserAsync(activeUser, CancellationToken.None))
            .ReturnsAsync(new IdentityUpdateResult(false, userUpdateErrors));

        var deactivateResult = await _authenticationService.DeactivateAccountAsync(Email, CancellationToken.None);

        var failedDeactivationResult = Assert.IsType<AccountDeActivationFailed>(deactivateResult);
        Assert.Equal(userUpdateErrors, failedDeactivationResult.Errors);

        _identityService.Verify(x => x.UpdateUserAsync(activeUser, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task DeactivateUser_WhenUserUpdateSucceeds_ReturnsAccountDeactivatedResult()
    {
        var activeUser = new User { Email = Email, IsActive = true };

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, CancellationToken.None))
            .ReturnsAsync(activeUser);

        _identityService
            .Setup(x => x.UpdateUserAsync(activeUser, CancellationToken.None))
            .ReturnsAsync(new IdentityUpdateResult(true));

        var deactivateResult = await _authenticationService.DeactivateAccountAsync(Email, CancellationToken.None);

        Assert.IsType<AccountDeactivated>(deactivateResult);

        _identityService.Verify(x => x.UpdateUserAsync(activeUser, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task DeactivateUser_WhenIdentityThrows_PropagatesException()
    {
        var exception = new InvalidOperationException("Unexpected error");

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() => _authenticationService.DeactivateAccountAsync(Email, CancellationToken.None));

        Assert.Equal("Unexpected error", result.Message);

        _identityService.Verify(
            x => x.UpdateUserAsync(
                It.IsAny<User>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReActivateUser_WhenUserDoesntExist_ReturnsAccountNotFound()
    {
        _identityService
            .Setup(x => x.FindByEmailAsync(Email, CancellationToken.None))
            .ReturnsAsync((User?)null);

        var activateUserResult = await _authenticationService.ReactivateAccountAsync(Email, CancellationToken.None);

        Assert.IsType<AccountNotFound>(activateUserResult);

        _identityService.Verify(x => x.UpdateUserAsync(It.IsAny<User>(), CancellationToken.None), Times.Never);
    }

    [Fact]
    public async Task ReactivateUser_WhenUserIsActive_ReturnsAlreadyActivatedResult()
    {
        var activeUser = new User { Email = Email, IsActive = true };

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, CancellationToken.None))
            .ReturnsAsync(activeUser);

        var activateAccountResult = await _authenticationService.ReactivateAccountAsync(Email, CancellationToken.None);

        Assert.IsType<AccountAlreadyActivated>(activateAccountResult);

        _identityService.Verify(x => x.UpdateUserAsync(activeUser, CancellationToken.None), Times.Never);
    }


    [Fact]
    public async Task ReActivateUser_WhenUserUpdateFails_ReturnsAccountDeActivationFailedResult()
    {
        var inActiveUser = new User { Email = Email, IsActive = false };
        var error = new IdentityError("Creation", "Something went wrong while updating the user");
        var errorList = new List<IdentityError>() { error };
        var userUpdateErrors = errorList.AsReadOnly();

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, CancellationToken.None))
            .ReturnsAsync(inActiveUser);

        _identityService
            .Setup(x => x.UpdateUserAsync(inActiveUser, CancellationToken.None))
            .ReturnsAsync(new IdentityUpdateResult(false, userUpdateErrors));

        var reactivateAccountResult = await _authenticationService.ReactivateAccountAsync(Email, CancellationToken.None);

        var failedReactivationResult = Assert.IsType<AccountActivationFailed>(reactivateAccountResult);
        Assert.Equal(userUpdateErrors, failedReactivationResult.Errors);

        _identityService.Verify(x => x.UpdateUserAsync(inActiveUser, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ReActivateUser_WhenUserUpdateSucceeds_ReturnsAccountDeactivatedResult()
    {
        var inactiveUser = new User { Email = Email, IsActive = false };

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, CancellationToken.None))
            .ReturnsAsync(inactiveUser);

        _identityService
            .Setup(x => x.UpdateUserAsync(inactiveUser, CancellationToken.None))
            .ReturnsAsync(new IdentityUpdateResult(true));

        var reactivateResult = await _authenticationService.ReactivateAccountAsync(Email, CancellationToken.None);

        Assert.IsType<AccountReactivated>(reactivateResult);

        _identityService.Verify(x => x.UpdateUserAsync(inactiveUser, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ReActivateUser_WhenIdentityThrows_PropagatesException()
    {
        var exception = new InvalidOperationException("Unexpected error");

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() => _authenticationService.ReactivateAccountAsync(Email, CancellationToken.None));

        Assert.Equal("Unexpected error", result.Message);

        _identityService.Verify(
            x => x.UpdateUserAsync(
                It.IsAny<User>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

}
