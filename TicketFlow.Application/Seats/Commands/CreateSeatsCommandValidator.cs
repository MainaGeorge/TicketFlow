using FluentValidation;

namespace TicketFlow.Application.Seats.Commands;

public class CreateSeatsCommandValidator :  AbstractValidator<CreateSeatsCommand>
{
    public CreateSeatsCommandValidator()
    {
        RuleFor(x => x.EventId).GreaterThan(0);
        RuleFor(x => x.Seats).NotEmpty();
        RuleFor(x => x.Seats)
            .Must(ContainsNoDuplicates)
            .WithMessage("Seats must not contain duplicates.");

        RuleForEach(x => x.Seats)
            .ChildRules(seat =>
            {
                seat.RuleFor(s => s.Number).GreaterThan(0);
                seat.RuleFor(f => f.Price).GreaterThan(0);
                seat.RuleFor(f => f.Row).NotEmpty().MaximumLength(10);
            });
    }

    private static bool ContainsNoDuplicates(IReadOnlyCollection<CreateSeatItem> seats)
    {
        var seenSeats = new HashSet<(string Row, int Number)>();

        foreach (var seat in seats)
        {
            if (string.IsNullOrWhiteSpace(seat.Row))
                continue;

            var identity = (seat.Row.Trim().ToUpperInvariant(), seat.Number);

            if (!seenSeats.Add(identity))
                return false;
        }

        return true;
    }
}
