using Microsoft.AspNetCore.Identity;
using Moq;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Domain.Entities;
using TicketFlow.Infrastructure.Services;

namespace TicketFlow.Tests.Infrastructure.Services;

public class IdentityServiceTests
{
    private readonly Mock<UserManager<User>> _userManager;
    private readonly IdentityService _identityService;
    private const string Email = "test-email@yahoo.com";

    public IdentityServiceTests()
    {
        var userStore = new Mock<IUserStore<User>>();
        _userManager = new Mock<UserManager<User>>(userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _identityService = new IdentityService(_userManager.Object);
    }

    [Fact]
    public async Task FindByEmailAsync_WhenUserDoesNotExist_ReturnsUser()
    {
        _userManager
            .Setup(x => x.FindByEmailAsync(Email))
            .ReturnsAsync((User?)null);

        var user = await _identityService.FindByEmailAsync(Email);

        Assert.Null(user);
        _userManager.Verify(x => x.FindByEmailAsync(Email), Times.Once);
    }

    [Fact]
    public async Task FindByEmailAsync_WhenUserExists_ReturnsUser()
    {
        var user = new User
        {
            Id = "user-1",
            Email = "test@email.com"
        };

        _userManager
            .Setup(x => x.FindByEmailAsync(user.Email))
            .ReturnsAsync(user);

        var result = await _identityService.FindByEmailAsync(
            user.Email);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);

        _userManager.Verify(x => x.FindByEmailAsync(user.Email), Times.Once);
    }

    [Fact]
    public async Task FindByEmailAsync_WhenManagerThrowsException_PropagatesException()
    {
        var message = "Something unexpected happened";
        var exception = new InvalidOperationException(message);

        var user = new User
        {
            Id = "user-1",
            Email = "test@email.com"
        };

        _userManager
            .Setup(x => x.FindByEmailAsync(user.Email))
            .ThrowsAsync(exception);

        var errorThrown = await Assert.ThrowsAsync<InvalidOperationException>(() => _identityService.FindByEmailAsync(user.Email));

        _userManager.Verify(x => x.FindByEmailAsync(user.Email), Times.Once);
    }

    [Fact]
    public async Task CheckPasswordAsync_WhenPasswordIsValid_ReturnsTrue()
    {
        var validPassword = "Someenferje$%^";
        var user = new User
        {
            Id = "user-1",
            Email = "test@email.com"
        };

        _userManager
            .Setup(x => x.CheckPasswordAsync(user, validPassword))
            .ReturnsAsync(true);

        var checkPassword = await _identityService.CheckPasswordAsync(user, validPassword);

        Assert.True(checkPassword);
        _userManager.Verify(x => x.CheckPasswordAsync(user, validPassword), Times.Once);
    }

    [Fact]
    public async Task CheckPasswordAsync_WhenPasswordIsInValid_ReturnsFalse()
    {
        var invalidPassword = "Someenferje$%^";
        var user = new User
        {
            Id = "user-1",
            Email = "test@email.com"
        };

        _userManager
            .Setup(x => x.CheckPasswordAsync(user, invalidPassword))
            .ReturnsAsync(false);

        var checkPassword = await _identityService.CheckPasswordAsync(user, invalidPassword);

        Assert.False(checkPassword);
        _userManager.Verify(x => x.CheckPasswordAsync(user, invalidPassword), Times.Once);

    }

    [Fact]
    public async Task CheckPasswordAsync_WhenUserManagerThrowsException_PropagatesException()
    {
        var message = "Something unexpected happened";
        var exception = new InvalidOperationException(message);

        var validPassword = "Someenferje$%^";
        var user = new User
        {
            Id = "user-1",
            Email = "test@email.com"
        };

        _userManager
            .Setup(x => x.CheckPasswordAsync(user, validPassword))
            .ThrowsAsync(exception);

        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(() => _identityService.CheckPasswordAsync(user, validPassword));

        Assert.Equal(message, thrownException.Message);
        _userManager.Verify(x => x.CheckPasswordAsync(user, validPassword), Times.Once);

    }

    [Fact]
    public async Task CreateUserAsync_WhenCreateUserSucceeds_ReturnsSuccessfulResult()
    {
        var user = new User
        {
            Id = "user-1",
            Email = "test@email.com"
        };

        var password = "testPassword";

        _userManager
            .Setup(x => x.CreateAsync(
                It.IsAny<User>(),
                It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var createUserResult = await _identityService.CreateUserAsync(user, password);

        var createdUser = Assert.IsType<IdentityCreationResult>(createUserResult);

        Assert.Equal(user.Id, createdUser.User!.Id);
        Assert.True(createdUser.Success);

        _userManager.Verify(x => x.CreateAsync(It.Is<User>(u => u.Email == user.Email && u.Id == user.Id), password), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_WhenCreateUserFails_ReturnsUnsuccessfulResultWithIdentityErrors()
    {
        var user = new User
        {
            Id = "user-1",
            Email = "test@email.com"
        };

        const string password = "testPassword";

        var identityError = new Microsoft.AspNetCore.Identity.IdentityError
        {
            Code = "DuplicateUserName",
            Description = "Username already exists."
        };

        var identityResult = IdentityResult.Failed(identityError);

        _userManager
            .Setup(x => x.CreateAsync(user, password))
            .ReturnsAsync(identityResult);

        var result = await _identityService.CreateUserAsync(user, password);

        Assert.False(result.Success);
        Assert.Same(user, result.User);

        var error = Assert.Single(result.Errors!);

        Assert.Equal(identityError.Code, error.Code);
        Assert.Equal(identityError.Description, error.Description);

        _userManager.Verify(x => x.CreateAsync(user, password), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_WhenUserManagerThrowsException_PropagatesException()
    {
        var message = "something unexpected happened";
        var exception = new InvalidOperationException(message);
        var user = new User
        {
            Id = "user-1",
            Email = "test@email.com"
        };

        const string password = "testPassword";

        _userManager
            .Setup(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ThrowsAsync(exception);

        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(() => _identityService.CreateUserAsync(user, password));
        Assert.Equal(message, thrownException.Message);

        _userManager.Verify(x => x.CreateAsync(user, password), Times.Once);
    }

    [Fact]
    public async Task UpdateUser_WhenUpdateUserSucceeds_ReturnsSuccessfulResult()
    {
        var user = new User
        {
            Id = "user-1",
            Email = "test@email.com"
        };

        _userManager
            .Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        var updateUserResult = await _identityService.UpdateUserAsync(user);

        var updatedUser = Assert.IsType<IdentityUpdateResult>(updateUserResult);

        Assert.True(updatedUser.Success);

        _userManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenUserUpdateFails_ReturnsUnsuccessfulResultWithIdentityErrors()
    {
        var user = new User
        {
            Id = "user-1",
            Email = "test@email.com"
        };

        var identityError = new Microsoft.AspNetCore.Identity.IdentityError
        {
            Code = "DuplicateUserName",
            Description = "Username already exists."
        };

        var identityResult = IdentityResult.Failed(identityError);

        _userManager
            .Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(identityResult);

        var result = await _identityService.UpdateUserAsync(user);

        Assert.False(result.Success);

        var error = Assert.Single(result.Errors!);

        Assert.Equal(identityError.Code, error.Code);
        Assert.Equal(identityError.Description, error.Description);

        _userManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenUserManagerThrowsException_PropagatesException()
    {
        var message = "something unexpected happened";
        var exception = new InvalidOperationException(message);
        var user = new User
        {
            Id = "user-1",
            Email = "test@email.com"
        };


        _userManager
            .Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ThrowsAsync(exception);

        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(() => _identityService.UpdateUserAsync(user));
        Assert.Equal(message, thrownException.Message);

        _userManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }
}
