using FluentValidation;

namespace TicketFlow.Application.Seats.Commands;

public class CreateSeatCommandValidator :  AbstractValidator<CreateSeatCommand>
{
    public CreateSeatCommandValidator()
    {
        RuleFor(f => f.Price).GreaterThan(0);
        RuleFor(f => f.Number).GreaterThan(0);
        RuleFor(f => f.Row).NotEmpty();
        RuleFor(f => f.EventId).GreaterThan(0);
    }
}
