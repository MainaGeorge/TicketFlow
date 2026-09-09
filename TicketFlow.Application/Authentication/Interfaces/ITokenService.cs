using TicketFlow.Contracts.DTOs;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Authentication.Interfaces;

public interface ITokenService
{
    Task<TokenResponse> GenerateTokensAsync(User user, CancellationToken cancellationToken);
}
