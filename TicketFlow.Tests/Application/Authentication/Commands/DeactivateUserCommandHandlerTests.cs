using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Abstractions.Authentication;
using TicketFlow.Application.Authentication.Commands.DeactivateUser;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Authentication.Commands;

public class DeactivateUserCommandHandlerTests
{
    private readonly Mock<IIdentityService> _identityService;
    private readonly Mock<ILogger<DeactivateUserCommandHandler>> _logger;
    private readonly DeactivateUserCommandHandler _handler;
    private const string Email = "test@email.com";

    public DeactivateUserCommandHandlerTests()
    {
        _identityService = new Mock<IIdentityService>();
        _logger = new Mock<ILogger<DeactivateUserCommandHandler>>();
        _handler = new DeactivateUserCommandHandler(_identityService.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_FindByEmailAsyncThrows_PropagatesException()
    {
        var exception = new InvalidOperationException("Unexpected error");
        var command = new DeactivateUserCommand(Email);

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));

        Assert.Equal("Unexpected error", result.Message);

        _identityService.Verify(x => x.FindByEmailAsync(Email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUpdateUserAsync_PropagatesException()
    {
        var exception = new InvalidOperationException("Unexpected error");
        var command = new DeactivateUserCommand(Email);
        var activeUser = new User { Email = Email, Id = "SomeId" };

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeUser);

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

        var command = new DeactivateUserCommand(Email);

        var activateUserResult = await _handler.Handle(command, CancellationToken.None);

        Assert.IsType<AccountNotFound>(activateUserResult);

        _identityService.Verify(x => x.FindByEmailAsync(Email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.UpdateUserAsync(It.IsAny<User>(), CancellationToken.None), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserUpdateFails_ReturnsAccountDeActivationFailedResult()
    {
        var activeUser = new User { Email = Email };
        var error = new IdentityError("Creation", "Something went wrong while updating the user");
        var errorList = new List<IdentityError>() { error };
        var userUpdateErrors = errorList.AsReadOnly();
        var command = new DeactivateUserCommand(Email);


        _identityService
            .Setup(x => x.FindByEmailAsync(Email, CancellationToken.None))
            .ReturnsAsync(activeUser);

        _identityService
            .Setup(x => x.UpdateUserAsync(activeUser, CancellationToken.None))
            .ReturnsAsync(new IdentityUpdateResult(false, userUpdateErrors));

        var deactivateResult = await _handler.Handle(command, CancellationToken.None);

        var failedDeactivationResult = Assert.IsType<AccountDeActivationFailed>(deactivateResult);
        Assert.Equal(userUpdateErrors, failedDeactivationResult.Errors);

        _identityService.Verify(x => x.FindByEmailAsync(Email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.UpdateUserAsync(activeUser, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserUpdateSucceeds_ReturnsAccountDeactivatedResult()
    {
        var activeUser = new User { Email = Email };
        var command = new DeactivateUserCommand(Email);

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, CancellationToken.None))
            .ReturnsAsync(activeUser);

        _identityService
            .Setup(x => x.UpdateUserAsync(activeUser, CancellationToken.None))
            .ReturnsAsync(new IdentityUpdateResult(true));

        var deactivateResult = await _handler.Handle(command, CancellationToken.None);

        Assert.IsType<AccountDeactivated>(deactivateResult);

        _identityService.Verify(x => x.FindByEmailAsync(Email, CancellationToken.None), Times.Once);
        _identityService.Verify(x => x.UpdateUserAsync(It.Is<User>(u => u.Email == activeUser.Email && !u.IsActive), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_PassesCancellationToken_ToIdentityService()
    {
        var activeUser = new User { Email = Email };
        var command = new DeactivateUserCommand(Email);
        using var source = new CancellationTokenSource();
        var token = source.Token;

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, token))
            .ReturnsAsync(activeUser);

        _identityService
            .Setup(x => x.UpdateUserAsync(activeUser, token))
            .ReturnsAsync(new IdentityUpdateResult(true));

        var deactivateResult = await _handler.Handle(command, token);

        _identityService.Verify(x => x.UpdateUserAsync(activeUser, token), Times.Once);
        _identityService.Verify(x => x.FindByEmailAsync(Email, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserIsAlreadyDeactivated_ReturnsAccountAlreadyDeactivated()
    {
        var inactiveUser = new User
        {
            Email = Email,
        };

        inactiveUser.Deactivate();

        _identityService
            .Setup(x => x.FindByEmailAsync(Email, CancellationToken.None))
            .ReturnsAsync(inactiveUser);

        var command = new DeactivateUserCommand(Email);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.IsType<AccountAlreadyDeactivated>(result);

        _identityService.Verify(x => x.FindByEmailAsync(Email, CancellationToken.None), Times.Once);

        _identityService.Verify(x => x.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
