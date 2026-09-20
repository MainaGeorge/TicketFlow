using FluentValidation.TestHelper;
using TicketFlow.Application.Bookings.Queries.GetBooking;

namespace TicketFlow.Tests.Application.Bookings.Queries.GetBooking;

public class GetBookingQueryValidatorTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task Validate_WhenBookingIdIsInValid_HasValidationError(int bookingId)
    {
        var query = new GetBookingQuery(bookingId, "user-123");
        var validator = new GetBookingQueryValidator();
        var results = await validator.TestValidateAsync(query);

        results.ShouldHaveValidationErrorFor(x => x.BookingId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData(null)]
    public async Task Validate_WhenUserIdIsInValid_HasValidationError(string? userId)
    {
        var query = new GetBookingQuery(1, userId!);
        var validator = new GetBookingQueryValidator();
        var results = await validator.TestValidateAsync(query);

        results.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public async Task Validate_WhenAllIsValid_HasNoValidationError()
    {
        var query = new GetBookingQuery(1, "user-123");
        var validator = new GetBookingQueryValidator();
        var results = await validator.TestValidateAsync(query);

        Assert.True(results.IsValid);
    }
}
