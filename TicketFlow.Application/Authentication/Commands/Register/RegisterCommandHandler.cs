using MediatR;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Abstractions.Authentication;
using TicketFlow.Application.Authentication.Models;

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

        if (createdUser.Success)
        {
            logger.LogInformation("User {Email} successfully registered", request.Email);
            return new RegistrationSucceeded(createdUser.User!);
        }

        logger.LogInformation("User {Email} registration failed", request.Email);
        return new RegistrationFailed(createdUser.Errors!);
    }
}
