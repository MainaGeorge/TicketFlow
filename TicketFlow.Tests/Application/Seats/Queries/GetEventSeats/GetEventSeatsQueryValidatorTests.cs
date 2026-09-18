using FluentValidation.TestHelper;
using TicketFlow.Application.Seats.Queries.GetEventSeats;

namespace TicketFlow.Tests.Application.Seats.Queries.GetEventSeats;

public class GetEventSeatsQueryValidatorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_WhenEventIdIsInvalid_HasValidationErrors(int eventId)
    {
        var query = new GetEventSeatsQuery(eventId);
        var validator = new GetEventSeatsQueryValidator();
        var results = await validator.TestValidateAsync(query);

        results.ShouldHaveValidationErrorFor(x => x.EventId);
    }

    [Fact]
    public async Task Validate_WhenAllIsValid_HasValidationErrors()
    {
        var query = new GetEventSeatsQuery(1);
        var validator = new GetEventSeatsQueryValidator();
        var results = await validator.TestValidateAsync(query);

        Assert.True(results.IsValid);
    }
}
