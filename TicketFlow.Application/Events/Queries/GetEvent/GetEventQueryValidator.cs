using FluentValidation;

namespace TicketFlow.Application.Events.Queries.GetEvent;

public class GetEventQueryValidator : AbstractValidator<GetEventQuery>
{
    public GetEventQueryValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
