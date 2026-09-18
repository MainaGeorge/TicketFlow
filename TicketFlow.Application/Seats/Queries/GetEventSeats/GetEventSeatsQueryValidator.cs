using FluentValidation;

namespace TicketFlow.Application.Seats.Queries.GetEventSeats;

public class GetEventSeatsQueryValidator : AbstractValidator<GetEventSeatsQuery>
{
    public GetEventSeatsQueryValidator()
    {
        RuleFor(x => x.EventId).GreaterThan(0);
    }
}
