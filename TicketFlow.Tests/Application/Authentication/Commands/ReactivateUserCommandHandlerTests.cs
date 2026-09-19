using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Authentication.Commands.ReactivateUser;
using TicketFlow.Application.Authentication.Interfaces;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Authentication.Commands;

public class ReactivateUserCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identityService;
    private readonly Mock<ILogger<ReactivateUserCommandHandler>> _logger;
    private readonly ReactivateUserCommandHandler _handler;
    private const string Email = "test@email.com";

    public ReactivateUserCommandHandlerTests()
    {
        _identityService = new Mock<IIdentityService>();
        _logger = new Mock<ILogger<ReactivateUserCommandHandler>>();
        _handler = new ReactivateUserCommandHandler(_identityService.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_FindByEmailAsyncThrows_PropagatesException()
    {
        var exception = new InvalidOperationException("Unexpected error");
        var command = new ReactivateUserCommand(Email);

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));

        Assert.Equal("Unexpected error", result.Message);

        _identityService.Verify(x => x.FindByEmailAsync(Email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUpdateUserAsyncThrows_PropagatesException()
    {
        var exception = new InvalidOperationException("Unexpected error");
        var command = new ReactivateUserCommand(Email);
        var inactiveUser = new User { Email = Email, IsActive = false, Id = "SomeId" };

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveUser);

        _identityService
            .Setup(x => x.UpdateUserAsync(It.Is<User>(e => e.Id == "SomeId" && e.Email == Email), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));

        Assert.Equal("Unexpected error", result.Message);

        _identityService.Verify(x => x.FindByEmailAsync(Email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserDoesntExist_ReturnsAccountNotFound()
    {
        _identityService
            .Setup(x => x.FindByEmailAsync(Email, CancellationToken.None))
            .ReturnsAsync((User?)null);

        var command = new ReactivateUserCommand(Email);

        var activateUserResult = await _handler.Handle(command, CancellationToken.None);

        Assert.IsType<AccountNotFound>(activateUserResult);

        _identityService.Verify(x => x.FindByEmailAsync(Email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.UpdateUserAsync(It.IsAny<User>(), CancellationToken.None), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserUpdateFails_ReturnsAccountReActivationFailedResult()
    {
        var inactiveUser = new User { Email = Email, IsActive = false };
        var error = new IdentityError("Creation", "Something went wrong while updating the user");
        var errorList = new List<IdentityError>() { error };
        var userUpdateErrors = errorList.AsReadOnly();
        var command = new ReactivateUserCommand(Email);


        _identityService
            .Setup(x => x.FindByEmailAsync(Email, CancellationToken.None))
            .ReturnsAsync(inactiveUser);

        _identityService
            .Setup(x => x.UpdateUserAsync(inactiveUser, CancellationToken.None))
            .ReturnsAsync(new IdentityUpdateResult(false, userUpdateErrors));

        var deactivateResult = await _handler.Handle(command, CancellationToken.None);

        var failedDeactivationResult = Assert.IsType<AccountActivationFailed>(deactivateResult);
        Assert.Equal(userUpdateErrors, failedDeactivationResult.Errors);

        _identityService.Verify(x => x.FindByEmailAsync(Email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.UpdateUserAsync(inactiveUser, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserUpdateSucceeds_ReturnsAccountReactivatedResult()
    {
        var inactiveUser = new User { Email = Email, IsActive = false };
        var command = new ReactivateUserCommand(Email);

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, CancellationToken.None))
            .ReturnsAsync(inactiveUser);

        _identityService
            .Setup(x => x.UpdateUserAsync(inactiveUser, CancellationToken.None))
            .ReturnsAsync(new IdentityUpdateResult(true));

        var deactivateResult = await _handler.Handle(command, CancellationToken.None);

        Assert.IsType<AccountReactivated>(deactivateResult);

        _identityService.Verify(x => x.FindByEmailAsync(Email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.UpdateUserAsync(It.Is<User>(u => u.IsActive && u.Email == inactiveUser.Email), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_PassesCancellationToken_ToIdentityService()
    {
        var inactiveUser = new User { Email = Email, IsActive = false };
        var command = new ReactivateUserCommand(Email);
        using var source = new CancellationTokenSource();
        var token = source.Token;

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, token))
            .ReturnsAsync(inactiveUser);

        _identityService
            .Setup(x => x.UpdateUserAsync(inactiveUser, token))
            .ReturnsAsync(new IdentityUpdateResult(true));

        var deactivateResult = await _handler.Handle(command, token);

        _identityService.Verify(x => x.UpdateUserAsync(inactiveUser, token), Times.Once);
        _identityService.Verify(x => x.FindByEmailAsync(Email, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserIsAlreadyActive_ReturnsAccountAlreadyActivated()
    {
        var activeUser = new User
        {
            Email = Email,
            IsActive = true
        };

        _identityService
            .Setup(x => x.FindByEmailAsync(Email,CancellationToken.None))
            .ReturnsAsync(activeUser);

        var command = new ReactivateUserCommand(Email);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.IsType<AccountAlreadyActivated>(result);

        _identityService.Verify(x => x.FindByEmailAsync(Email, CancellationToken.None), Times.Once);

        _identityService.Verify(x => x.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
