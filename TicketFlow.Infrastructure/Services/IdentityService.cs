using Microsoft.AspNetCore.Identity;
using TicketFlow.Application.Authentication.Interfaces;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Services;

public class IdentityService(UserManager<User> userManager) : IIdentityService
{
    public async Task<bool> CheckPasswordAsync(User user, string password, CancellationToken cancellationToken = default)
    {
        return await userManager.CheckPasswordAsync(user, password);
    }

    public async Task<IdentityCreationResult> CreateUserAsync(User user, string password, CancellationToken cancellationToken = default)
    {
        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(x => new Application.Authentication.Models.IdentityError(x.Code, x.Description)).ToList().AsReadOnly();
            return new IdentityCreationResult(Success: false, user, errors);
        }

        return new IdentityCreationResult(Success: true, user);
    }
    
    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await userManager.FindByEmailAsync(email);
    }

    public async Task<IdentityUpdateResult> UpdateUserAsync(User user, CancellationToken cancellationToken)
    {
        var updateResult = await userManager.UpdateAsync(user);

        if(updateResult.Succeeded)
            return new IdentityUpdateResult(Success: true);

        var errors = updateResult.Errors
            .Select(x => new Application.Authentication.Models.IdentityError(
                x.Code,
                x.Description))
            .ToList()
            .AsReadOnly();

        return new IdentityUpdateResult(Success: false, errors);
    }
}
