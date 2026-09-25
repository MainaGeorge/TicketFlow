using MediatR;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Abstractions.Authentication;
using TicketFlow.Application.Abstractions.Repositories;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Authentication.Commands.Login;

public class LoginCommandHandler(
    IIdentityService identityService,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepository,
    ILogger<LoginCommandHandler> logger) 
    : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await identityService.FindByEmailAsync(request.Email, cancellationToken);

        if (user is null)
        {
            logger.LogInformation("user {Email} attempted to log in but the account does not exist", request.Email);
            return new InvalidCredentials();
        }

        if (!user.IsActive)
        {
            logger.LogInformation("user {Email} attempted to log in but the account is deactivated.", request.Email);
            return new InvalidCredentials();
        }

        var validPassword = await identityService.CheckPasswordAsync(user, request.Password, cancellationToken);

        if (!validPassword)
        {
            logger.LogInformation("user {Email} attempted to log in with invalid credentials", request.Email);
            return new InvalidCredentials();
        }

        var tokens = await tokenService.GenerateTokensAsync(user, cancellationToken);

        var refreshToken = new TicketFlow.Domain.Entities.RefreshToken
        {
            Token = tokens.RefreshToken,
            UserId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("user {Email} successfully logged in", request.Email);

        return new LoginSucceeded(tokens);
    }
}
