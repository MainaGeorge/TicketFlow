using FluentValidation.TestHelper;
using TicketFlow.Application.Bookings.Commands.CreateBooking;

namespace TicketFlow.Tests.Application.Bookings.Commands.CreateBooking;

public class CreateBookingCommandValidatorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_WhenEventIdIsNotValid_HasValidationError(int eventId)
    {
        var command = new CreateBookingCommand(eventId, 1, "user-id");
        var validator = new CreateBookingCommandValidator();
        var result = await validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.EventId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_WhenSeatIdIsNotValid_HasValidationError(int seatId)
    {
        var command = new CreateBookingCommand(1, seatId, "user-id");
        var validator = new CreateBookingCommandValidator();
        var result = await validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.SeatId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData(null)]
    public async Task Validate_WhenUserIdIsNotValid_HasValidationError(string? userId)
    {
        var command = new CreateBookingCommand(1, 1, userId);
        var validator = new CreateBookingCommandValidator();
        var result = await validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public async Task Validate_WhenAllFieldsAreValid_HasNoValidationError()
    {
        var command = new CreateBookingCommand(1, 1, "user-id");
        var validator = new CreateBookingCommandValidator();
        var result = await validator.TestValidateAsync(command);

        Assert.True(result.IsValid);
    }
}
