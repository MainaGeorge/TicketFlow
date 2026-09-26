using MediatR;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Abstractions.Authentication;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Application.Authorization;

namespace TicketFlow.Application.Authentication.Commands.Register;

public class RegisterCommandHandler(IIdentityService identityService, ILogger<RegisterCommandHandler> logger) : IRequestHandler<RegisterCommand, RegisterResult>
{
    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await identityService.FindByEmailAsync(request.Email, cancellationToken);

        if (existingUser is not null)
        {
            logger.LogInformation("user {Email} attempted to register but the email is already in use.", request.Email);
            return new EmailAlreadyRegistered();
        }

        var user = new Domain.Entities.User
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        var createdUser = await identityService.CreateUserAsync(user, request.Password, cancellationToken);

        if (!createdUser.Success)
        {
            logger.LogInformation("User {Email} registration failed", request.Email);
            return new RegistrationFailed(createdUser.Errors!);
        }

        var assignRoleResult = await identityService.AddToRoleAsync(createdUser.User!, Roles.User, cancellationToken);

        if (!assignRoleResult.Success)
        {
            logger.LogWarning("Failed to assign {Email} to role {Role}", request.Email, Roles.User);
            return new RegistrationFailed(assignRoleResult.Errors!);
        }

        logger.LogInformation("User {Email} successfully registered", request.Email);
        return new RegistrationSucceeded(createdUser.User!);
    }
}
