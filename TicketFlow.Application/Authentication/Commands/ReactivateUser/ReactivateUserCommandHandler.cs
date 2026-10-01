using MediatR;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Abstractions.Authentication;
using TicketFlow.Application.Authentication.Models;

namespace TicketFlow.Application.Authentication.Commands.ReactivateUser;

public class ReactivateUserCommandHandler(
    IIdentityService identityService,
    ILogger<ReactivateUserCommandHandler> logger) 
    : IRequestHandler<ReactivateUserCommand, AccountResult>
{
    public async Task<AccountResult> Handle(ReactivateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await identityService.FindByEmailAsync(request.Email, cancellationToken);

        if (user is null)
        {
            logger.LogInformation("User {Email} not found", request.Email);
            return new AccountNotFound();
        }

        if (user.IsActive)
        {
            logger.LogInformation("User {Email} is already activated", request.Email);
            return new AccountAlreadyActivated();
        }

        user.Reactivate();
        var updatedUser = await identityService.UpdateUserAsync(user, cancellationToken);

        if (updatedUser.Success)
        {
            logger.LogInformation("User {Email} successfully reactivated", request.Email);
            return new AccountReactivated();
        }

        logger.LogInformation("User {Email} reactivation failed", request.Email);
        return new AccountActivationFailed(updatedUser.Errors);
    }
}
