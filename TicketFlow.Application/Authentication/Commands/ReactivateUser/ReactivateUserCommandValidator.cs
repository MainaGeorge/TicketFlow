using FluentValidation;

namespace TicketFlow.Application.Authentication.Commands.ReactivateUser;

public class ReactivateUserCommandValidator : AbstractValidator<ReactivateUserCommand>
{
    public ReactivateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
