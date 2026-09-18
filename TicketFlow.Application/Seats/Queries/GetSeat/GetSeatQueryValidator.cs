using FluentValidation;

namespace TicketFlow.Application.Seats.Queries.GetSeat;

public class GetSeatQueryValidator : AbstractValidator<GetSeatQuery>
{
    public GetSeatQueryValidator()
    {
        RuleFor(x => x.EventId).GreaterThan(0);
        RuleFor(x => x.SeatId).GreaterThan(0);
    }
}
