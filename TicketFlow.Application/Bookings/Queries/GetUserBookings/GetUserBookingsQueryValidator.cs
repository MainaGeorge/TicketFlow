using FluentValidation;

namespace TicketFlow.Application.Bookings.Queries.GetUserBookings;

public class GetUserBookingsQueryValidator : AbstractValidator<GetUserBookingsQuery>
{
    public GetUserBookingsQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
