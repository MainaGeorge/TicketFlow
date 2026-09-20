using TicketFlow.Application.Seats.Commands;
using FluentValidation.TestHelper;

namespace TicketFlow.Tests.Application.Seats.Commands;

public class CreateSeatCommandValidatorTests
{
    [Fact]
    public async Task Validate_WhenPriceIsZero_HasValidationError()
    {
        var validator = new CreateSeatCommandValidator();
        var command = new CreateSeatCommand(2, "A", 3, 0m);

        var results = await validator.TestValidateAsync(command);

        results.ShouldHaveValidationErrorFor(x => x.Price);
    }

    [Fact]
    public async Task Validate_WhenPriceIsGreaterThanZero_HasNoValidationErrorForPrice()
    {
        var validator = new CreateSeatCommandValidator();
        var command = new CreateSeatCommand(2, "A", 3, 0.01m);

        var results = await validator.TestValidateAsync(command);

        results.ShouldNotHaveValidationErrorFor(x => x.Price);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_WhenNumberIsNotGreaterThanZero_HasValidationError(int number)
    {
        var validator = new CreateSeatCommandValidator();
        var command = new CreateSeatCommand(2, "A", number, 0.01m);

        var results = await validator.TestValidateAsync(command);

        results.ShouldHaveValidationErrorFor(x => x.Number);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_WhenEventIdIsNotGreaterThanZero_HasValidationError(int eventId)
    {
        var validator = new CreateSeatCommandValidator();
        var command = new CreateSeatCommand(eventId, "A", 3, 0.01m);

        var results = await validator.TestValidateAsync(command);

        results.ShouldHaveValidationErrorFor(x => x.EventId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("    ")]
    public async Task Validate_WhenRowIsNullOrWhitespace_HasValidationError(string? row)
    {
        var validator = new CreateSeatCommandValidator();
        var command = new CreateSeatCommand(1, row!, 3, 0.01m);

        var results = await validator.TestValidateAsync(command);

        results.ShouldHaveValidationErrorFor(x => x.Row);
    }

    [Fact]
    public async Task Validate_WhenRowHasMoreThan10Characters_HasValidationError()
    {
        var validator = new CreateSeatCommandValidator();
        var command = new CreateSeatCommand(1, new string('r', 11), 3, 0.01m);

        var results = await validator.TestValidateAsync(command);
        results.ShouldHaveValidationErrorFor(x => x.Row);
    }

    [Fact]
    public async Task Validate_WhenAllIsValid_HasNoValidationError()
    {
        var validator = new CreateSeatCommandValidator();
        var command = new CreateSeatCommand(1, "C", 3, 0.01m);

        var results = await validator.TestValidateAsync(command);

        Assert.True(results.IsValid);
    }
}
