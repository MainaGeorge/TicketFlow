using FluentValidation.TestHelper;
using TicketFlow.Application.Bookings.Queries.GetUserBookings;

namespace TicketFlow.Tests.Application.Bookings.Queries.GetUserBooking;

public class GetUserBookingsValidatorTests
{
    [Theory]
    [InlineData("    ")]
    [InlineData(null)]
    [InlineData("")]
    public async Task Validate_WhenUserIdIsInvalid_HasValidationErrors(string? userId)
    {
        var query = new GetUserBookingsQuery(userId);
        var validator = new GetUserBookingsQueryValidator();
        var results = await validator.TestValidateAsync(query);

        results.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public async Task Validate_WhenAllIsValid_HasNoValidationErros()
    {
        var query = new GetUserBookingsQuery("userId");
        var validator = new GetUserBookingsQueryValidator();
        var results = await validator.TestValidateAsync(query);

        Assert.True(results.IsValid);
    }
}
