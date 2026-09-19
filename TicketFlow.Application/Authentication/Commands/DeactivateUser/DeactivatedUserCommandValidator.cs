using FluentValidation;

namespace TicketFlow.Application.Authentication.Commands.DeactivateUser;

public class DeactivatedUserCommandValidator : AbstractValidator<DeactivateUserCommand>
{
    public DeactivatedUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
