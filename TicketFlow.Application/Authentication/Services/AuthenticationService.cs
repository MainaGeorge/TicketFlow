using Microsoft.Extensions.Logging;
using TicketFlow.Application.Authentication.Interfaces;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Contracts.DTOs;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Authentication.Services;

public class AuthenticationService(
    IIdentityService identityService,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepository,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    public async Task<AccountResult> DeactivateAccountAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await identityService.FindByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            logger.LogInformation("User {Email} not found", email);
            return new AccountNotFound();
        }

        if (!user.IsActive)
        {
            logger.LogInformation("User {Email} is already deactivated", email);
            return new AccountAlreadyDeactivated();
        }

        user.IsActive = false;
        var updatedUser = await identityService.UpdateUserAsync(user, cancellationToken);

        if (updatedUser.Success)
        {
            logger.LogInformation("User {Email} successfully deactivated", email);
            return new AccountDeactivated();
        }

        logger.LogInformation("User {Email} deactivation failed", email);
        return new AccountDeActivationFailed(updatedUser.Errors);
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await identityService.FindByEmailAsync(request.Email, cancellationToken);

        if(user is null)
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

        var refreshToken = new RefreshToken
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

    public async Task<AccountResult> ReactivateAccountAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await identityService.FindByEmailAsync(email, cancellationToken);

        if(user is null)
        {
            logger.LogInformation("User {Email} not found", email);
            return new AccountNotFound();
        }

        if (user.IsActive)
        {
            logger.LogInformation("User {Email} is already activated", email);
            return new AccountAlreadyActivated();
        }

        user.IsActive = true;
        var updatedUser = await identityService.UpdateUserAsync(user, cancellationToken);

        if (updatedUser.Success)
        {
            logger.LogInformation("User {Email} successfully reactivated", email);
            return new AccountReactivated();
        }

        logger.LogInformation("User {Email} reactivation failed", email);
        return new AccountActivationFailed(updatedUser.Errors);
    }

    public async Task<RefreshTokenResult> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var refreshToken = await refreshTokenRepository.GetByTokenAsync(request.RefreshToken, cancellationToken);

        if (refreshToken is null)
            return new RefreshTokenInvalid();

        if (refreshToken.IsRevoked)
            return new RefreshTokenInvalid();

        if (refreshToken.IsExpired)
            return new RefreshTokenInvalid();

        if (refreshToken.User is null || !refreshToken.User.IsActive)
            return new RefreshTokenInvalid();

        var tokens = await tokenService.GenerateTokensAsync(refreshToken.User!, cancellationToken);

        refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        refreshToken.ReplacedBy = tokens.RefreshToken;

        var replacementToken = new RefreshToken
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

    public async Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var existingUser = await identityService.FindByEmailAsync(request.Email, cancellationToken);

        if(existingUser is not null)
        {
            logger.LogInformation("user {Email} attempted to register but the email is already in use.", request.Email);
            return new EmailAlreadyRegistered();
        }

        var user = new Domain.Entities.User
        {
            UserName = request.Email,
            Email = request.Email,
            IsActive = true,
            DisplayName = request.DisplayName
        };

        var createdUser = await identityService.CreateUserAsync(user, request.Password, cancellationToken);

        if (createdUser.Success)
        {
            logger.LogInformation("User {Email} successfully registered", request.Email);
            return new RegistrationSucceeded(createdUser.User!);
        }

        logger.LogInformation("User {Email} registration failed", request.Email);
        return new RegistrationFailed(createdUser.Errors!);
    }
}
