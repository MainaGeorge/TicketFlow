using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Authentication.Commands.Register;
using TicketFlow.Application.Authentication.Interfaces;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Authentication.Commands;

public class RegisterCommandHandlerTests
{
    private readonly Mock<ILogger<RegisterCommandHandler>> _logger;
    private readonly Mock<IIdentityService> _identityService;
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        _logger = new Mock<ILogger<RegisterCommandHandler>>();
        _identityService = new Mock<IIdentityService>();
        _handler = new RegisterCommandHandler(_identityService.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_WhenEmailExists_ReturnsAlreadyRegistered()
    {
        var email = "test@user.com";
        string password = "password";
        string displayName = "testDisplayName";

        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Email = email });

        var registrationResult = await _handler.Handle(new RegisterCommand(email, password, displayName), CancellationToken.None);

        var emailRegisteredResult = Assert.IsType<EmailAlreadyRegistered>(registrationResult);

        _identityService.Verify(x => x.FindByEmailAsync(email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.CreateUserAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenIdentityCreatesUser_ReturnsRegistrationSucceeded()
    {
        var (email, password, displayName) = ("test@user.com", "password", "testDisplayName");
        var user = new User { Email = email, UserName = email, DisplayName = displayName, IsActive = true };
        var registerCommand = new RegisterCommand(email, password, displayName);
        var identityCreationResult = new IdentityCreationResult(true, user, null);


        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _identityService
            .Setup(x => x.CreateUserAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(identityCreationResult);

        var registrationResult = await _handler.Handle(registerCommand, CancellationToken.None);

        var userRegistrationResult = Assert.IsType<RegistrationSucceeded>(registrationResult);

        Assert.NotNull(userRegistrationResult.User);
        Assert.Equal(user.Email, userRegistrationResult.User.Email);
        Assert.Equal(user.IsActive, userRegistrationResult.User.IsActive);
        Assert.Equal(user.DisplayName, userRegistrationResult.User.DisplayName);

        _identityService.Verify(x => x.FindByEmailAsync(email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.CreateUserAsync(
            It.Is<User>(u => u.IsActive == user.IsActive && u.Email == email && u.DisplayName == displayName && u.UserName == email && u.IsActive),
            password, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenIdentityFailsToCreateUser_ReturnsRegistrationFailed()
    {
        var error = new IdentityError("Creation", "Something went wrong while creating the user");
        var errorList = new List<IdentityError>() { error };
        var userCreationErrors = errorList.AsReadOnly();
        var (email, password, displayName) = ("test@user.com", "password", "testDisplayName");
        var registerCommand = new RegisterCommand(email, password, displayName);


        var user = new User { Email = email };
        var identityFailureResult = new IdentityCreationResult(Success: false, User: null, Errors: userCreationErrors);

        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _identityService
            .Setup(x => x.CreateUserAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(identityFailureResult);

        var registrationResult = await _handler.Handle(registerCommand, CancellationToken.None);

        var userRegistrationResult = Assert.IsType<RegistrationFailed>(registrationResult);

        Assert.NotEmpty(userRegistrationResult.Errors);
        Assert.Equal(userCreationErrors, userRegistrationResult.Errors);

        _identityService.Verify(x => x.FindByEmailAsync(email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.CreateUserAsync(It.Is<User>(u => u.Email == email), password, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenFindByEmailThrows_PropagatesException()
    {
        var message = "Something unexpected happened during find by email async execution";
        var exception = new InvalidOperationException(message);
        var (email, password, displayName) = ("test@user.com", "password", "testDisplayName");
        var registerCommand = new RegisterCommand(email, password, displayName);


        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(registerCommand, CancellationToken.None));

        Assert.Same(exception, error);
        _identityService.Verify(x => x.FindByEmailAsync(email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.CreateUserAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCreateUserThrows_PropagatesException()
    {
        var (email, password, displayName) = ("test@user.com", "password", "testDisplayName");
        var registerCommand = new RegisterCommand(email, password, displayName);

        _identityService
            .Setup(x => x.FindByEmailAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var exception = new InvalidOperationException("Identity failure");

        _identityService
            .Setup(x => x.CreateUserAsync(
                It.IsAny<User>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(registerCommand, CancellationToken.None));

        Assert.Same(exception, error);
        _identityService.Verify(x => x.FindByEmailAsync(email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.CreateUserAsync(It.Is<User>(e => e.Email == email), password, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToIdentityService()
    {
        var (email, password, displayName) = ("test@user.com", "password", "testDisplayName");
        var user = new User { Email = email, UserName = email, DisplayName = displayName, IsActive = true };
        var registerCommand = new RegisterCommand(email, password, displayName);
        var identityCreationResult = new IdentityCreationResult(true, user, null);
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;


        _identityService
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _identityService
            .Setup(x => x.CreateUserAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(identityCreationResult);

        var registrationResult = await _handler.Handle(registerCommand, token);

        var userRegistrationResult = Assert.IsType<RegistrationSucceeded>(registrationResult);


        _identityService.Verify(x => x.FindByEmailAsync(email, token), Times.Once);
        _identityService.Verify(x => x.CreateUserAsync(
            It.Is<User>(u => u.IsActive == user.IsActive && u.Email == email && u.DisplayName == displayName),
            password, token), Times.Once);
    }
}
