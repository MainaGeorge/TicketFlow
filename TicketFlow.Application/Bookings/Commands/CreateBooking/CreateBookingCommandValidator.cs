using FluentValidation;

namespace TicketFlow.Application.Bookings.Commands.CreateBooking;

public class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.EventId).GreaterThan(0);
        RuleFor(x => x.SeatId).GreaterThan(0);
    }
}
