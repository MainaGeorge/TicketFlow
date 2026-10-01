using MediatR;
using TicketFlow.Application.Abstractions.Authentication;
using TicketFlow.Application.Abstractions.Repositories;
using TicketFlow.Application.Authentication.Models;

namespace TicketFlow.Application.Authentication.Commands.RefreshToken;

public class RefreshTokenCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IIdentityService identityService,
    ITokenService tokenService) : IRequestHandler<RefreshTokenCommand, RefreshTokenResult>
{
    public async Task<RefreshTokenResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var refreshToken = await refreshTokenRepository.GetByTokenAsync(request.Token, cancellationToken);

        if (refreshToken is null)
            return new RefreshTokenInvalid();

        if (refreshToken.IsRevoked)
            return new RefreshTokenInvalid();

        if (refreshToken.IsExpired)
            return new RefreshTokenInvalid();

        if (refreshToken.User is null || !refreshToken.User.IsActive)
            return new RefreshTokenInvalid();

        var roles = await identityService.GetRolesAsync(refreshToken.User, cancellationToken);

        var tokens = await tokenService.GenerateTokensAsync(refreshToken.User, roles, cancellationToken);

        refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        refreshToken.ReplacedBy = tokens.RefreshToken;

        var replacementToken = new Domain.Entities.RefreshToken
        {
            Token = tokens.RefreshToken,
            UserId = refreshToken.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        await refreshTokenRepository.AddAsync(replacementToken, cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return new RefreshTokenSucceeded(tokens);
    }
}
