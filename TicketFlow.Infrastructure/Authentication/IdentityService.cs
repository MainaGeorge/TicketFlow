using Microsoft.AspNetCore.Identity;
using TicketFlow.Application.Abstractions.Authentication;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Domain.Entities;
using IdentityError = TicketFlow.Application.Authentication.Models.IdentityError;

namespace TicketFlow.Infrastructure.Authentication;

public class IdentityService(UserManager<User> userManager) : IIdentityService
{
    public Task<bool> CheckPasswordAsync(User user, string password, CancellationToken cancellationToken = default)
        => userManager.CheckPasswordAsync(user, password);

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        => userManager.FindByEmailAsync(email);

    public async Task<IdentityCreationResult> CreateUserAsync(User user, string password, CancellationToken cancellationToken = default)
    {
        var result = await userManager.CreateAsync(user, password);

        if (result.Succeeded)
            return new IdentityCreationResult(Success: true, user);

        var errors = result.Errors
            .Select(x => new IdentityError(
                x.Code, 
                x.Description))
            .ToList()
            .AsReadOnly();

        return new IdentityCreationResult(Success: false, user, errors);
    }

    public async Task<IdentityUpdateResult> UpdateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        var updateResult = await userManager.UpdateAsync(user);

        if (updateResult.Succeeded)
            return new IdentityUpdateResult(Success: true);

        var errors = updateResult.Errors
            .Select(x => new IdentityError(
                x.Code,
                x.Description))
            .ToList()
            .AsReadOnly();

        return new IdentityUpdateResult(Success: false, errors);
    }

    public Task<IList<string>> GetRolesAsync(User user, CancellationToken cancellationToken = default) => userManager.GetRolesAsync(user);

    public async Task<IdentityUpdateResult> AddToRoleAsync(User user, string role, CancellationToken cancellationToken = default)
    {
        var identityResult = await userManager.AddToRoleAsync(user, role);

        if (identityResult.Succeeded)
            return new IdentityUpdateResult(Success: true);

        var errors = identityResult.Errors
            .Select(x => new IdentityError(
                x.Code,
                x.Description))
            .ToList()
            .AsReadOnly();

        return new IdentityUpdateResult(Success: false, errors);
    }
}
