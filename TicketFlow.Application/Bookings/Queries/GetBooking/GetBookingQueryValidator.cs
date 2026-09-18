using FluentValidation;

namespace TicketFlow.Application.Bookings.Queries.GetBooking;

public class GetBookingQueryValidator : AbstractValidator<GetBookingQuery>
{
    public GetBookingQueryValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.UserId).NotEmpty();
    }
}
