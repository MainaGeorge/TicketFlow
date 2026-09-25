using MediatR;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Abstractions.Authentication;
using TicketFlow.Application.Authentication.Models;

namespace TicketFlow.Application.Authentication.Commands.DeactivateUser;

public class DeactivateUserCommandHandler(
    IIdentityService identityService,
    ILogger<DeactivateUserCommandHandler> logger) 
    : IRequestHandler<DeactivateUserCommand, AccountResult>
{
    public async Task<AccountResult> Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await identityService.FindByEmailAsync(request.Email, cancellationToken);

        if (user is null)
        {
            logger.LogInformation("User {Email} not found", request.Email);
            return new AccountNotFound();
        }

        if (!user.IsActive)
        {
            logger.LogInformation("User {Email} is already deactivated", request.Email);
            return new AccountAlreadyDeactivated();
        }

        user.Deactivate();
        var updatedUser = await identityService.UpdateUserAsync(user, cancellationToken);

        if (updatedUser.Success)
        {
            logger.LogInformation("User {Email} successfully deactivated", request.Email);
            return new AccountDeactivated();
        }

        logger.LogInformation("User {Email} deactivation failed", request.Email);
        return new AccountDeActivationFailed(updatedUser.Errors);
    }
}
