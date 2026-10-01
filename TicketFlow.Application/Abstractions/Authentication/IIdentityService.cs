using TicketFlow.Application.Authentication.Models;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Abstractions.Authentication;

public interface IIdentityService
{
    Task<IdentityCreationResult> CreateUserAsync(User user, string password, CancellationToken cancellationToken = default);
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> CheckPasswordAsync(User user, string password, CancellationToken cancellationToken = default);
    Task<IdentityUpdateResult> UpdateUserAsync(User user, CancellationToken cancellationToken = default);
    Task<IList<string>> GetRolesAsync(User user, CancellationToken cancellationToken = default);
    Task<IdentityUpdateResult> AddToRoleAsync(User user, string role, CancellationToken cancellationToken = default);
}
