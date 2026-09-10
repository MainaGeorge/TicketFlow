using TicketFlow.Application.Authentication.Models;
using TicketFlow.Contracts.DTOs;

namespace TicketFlow.Application.Authentication.Interfaces;

public interface IAuthenticationService
{
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AccountResult> DeactivateAccountAsync(string email, CancellationToken cancellationToken = default);
    Task<AccountResult> ReactivateAccountAsync(string email, CancellationToken cancellationToken = default);
}
