using FluentValidation;

namespace TicketFlow.Application.Events.Commands.CreateEvent;

public class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventCommandValidator()
    {
        RuleFor(x => x.Venue).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Date).NotEmpty();
    }
}
