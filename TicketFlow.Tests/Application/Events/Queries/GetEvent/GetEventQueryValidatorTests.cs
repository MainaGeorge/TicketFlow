using FluentValidation.TestHelper;
using TicketFlow.Application.Events.Queries.GetEvent;

namespace TicketFlow.Tests.Application.Events.Queries.GetEvent;

public class GetEventQueryValidatorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_WhenIdIsInValid_HasNoValidationErrors(int eventId)
    {
        var query = new GetEventQuery(eventId);
        var validator = new GetEventQueryValidator();

        var results = await validator.TestValidateAsync(query);
        results.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public async Task Validate_WhenIdIsValid_HasNoValidationErrors()
    {
        var query = new GetEventQuery(1);
        var validator = new GetEventQueryValidator();

        var results = await validator.TestValidateAsync(query);
        Assert.True(results.IsValid);
    }
}
