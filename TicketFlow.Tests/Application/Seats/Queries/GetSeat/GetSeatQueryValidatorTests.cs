using FluentValidation.TestHelper;
using TicketFlow.Application.Seats.Queries.GetSeat;

namespace TicketFlow.Tests.Application.Seats.Queries.GetSeat;

public class GetSeatQueryValidatorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_WhenEventIdIsInvalid_HasValidationErrors(int eventId)
    {
        var validator = new GetSeatQueryValidator();
        var query = new GetSeatQuery(eventId, 2);
        var results = await validator.TestValidateAsync(query);

        results.ShouldHaveValidationErrorFor(x =>  x.EventId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_WhenSeatIdIsInvalid_HasValidationErrors(int seatId)
    {
        var validator = new GetSeatQueryValidator();
        var query = new GetSeatQuery(2, seatId);
        var results = await validator.TestValidateAsync(query);

        results.ShouldHaveValidationErrorFor(x => x.SeatId);
    }

    [Fact]
    public async Task Validate_WhenAllIsvalid_HasNoValidationErrors()
    {
        var validator = new GetSeatQueryValidator();
        var query = new GetSeatQuery(2, 2);
        var results = await validator.TestValidateAsync(query);

        Assert.True(results.IsValid);
    }
}
