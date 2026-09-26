using TicketFlow.Application.Seats.Commands;
using FluentValidation.TestHelper;

namespace TicketFlow.Tests.Application.Seats.Commands;

public class CreateSeatCommandValidatorTests
{
    [Fact]
    public async Task Validate_WhenSeatsIsEmpty_HasValidationError()
    {
        var validator = new CreateSeatsCommandValidator();
        var command = new CreateSeatsCommand(2, Array.Empty<CreateSeatItem>());

        var results = await validator.TestValidateAsync(command);

        results.ShouldHaveValidationErrorFor(x => x.Seats);
    }

    [Fact]
    public async Task Validate_WhenPriceIsZero_HasValidationError()
    {
        var validator = new CreateSeatsCommandValidator();
        var seats = new List<CreateSeatItem> { new("A", 4, 10m), new("A", 3, 0m) };
        var command = new CreateSeatsCommand(2, seats.AsReadOnly());

        var results = await validator.TestValidateAsync(command);

        results.ShouldHaveValidationErrorFor("Seats[1].Price");
    }

    [Fact]
    public async Task Validate_WhenPriceIsGreaterThanZero_HasNoValidationErrorForPrice()
    {
        var validator = new CreateSeatsCommandValidator();
        var seats = new List<CreateSeatItem> { new("A", 3, 0.01m), new("A", 4, 10m) };
        var command = new CreateSeatsCommand(2, seats.AsReadOnly());

        var results = await validator.TestValidateAsync(command);

        results.ShouldNotHaveValidationErrorFor("Seats[0].Price");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_WhenNumberIsNotGreaterThanZero_HasValidationError(int number)
    {
        var validator = new CreateSeatsCommandValidator();
        var seats = new List<CreateSeatItem> { new("A", 4, 10m), new("B", 4, 10m), new("A", number, 50m) };
        var command = new CreateSeatsCommand(2, seats.AsReadOnly());

        var results = await validator.TestValidateAsync(command);

        results.ShouldHaveValidationErrorFor("Seats[2].Number");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_WhenEventIdIsNotGreaterThanZero_HasValidationError(int eventId)
    {
        var validator = new CreateSeatsCommandValidator();
        var seats = new List<CreateSeatItem> { new("A", 3, 10m) };
        var command = new CreateSeatsCommand(eventId, seats.AsReadOnly());

        var results = await validator.TestValidateAsync(command);

        results.ShouldHaveValidationErrorFor(x => x.EventId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("    ")]
    public async Task Validate_WhenRowIsNullOrWhitespace_HasValidationError(string? row)
    {
        var validator = new CreateSeatsCommandValidator();
        var seats = new List<CreateSeatItem> { new(row!, 3, 10m) };
        var command = new CreateSeatsCommand(2, seats.AsReadOnly());

        var results = await validator.TestValidateAsync(command);

        results.ShouldHaveValidationErrorFor("Seats[0].Row");
    }

    [Fact]
    public async Task Validate_WhenRowHasMoreThan10Characters_HasValidationError()
    {
        var validator = new CreateSeatsCommandValidator();
        var seats = new List<CreateSeatItem> { new(new string('r', 11), 3, 10m) };
        var command = new CreateSeatsCommand(2, seats.AsReadOnly());

        var results = await validator.TestValidateAsync(command);
        results.ShouldHaveValidationErrorFor("Seats[0].Row");
    }

    [Fact]
    public async Task Validate_WhenSeatsContainExactDuplicate_HasValidationError()
    {
        var validator = new CreateSeatsCommandValidator();
        var seats = new List<CreateSeatItem> { new("A", 10, 15m), new("A", 10, 15m) };
        var command = new CreateSeatsCommand(2, seats.AsReadOnly());

        var results = await validator.TestValidateAsync(command);

        results.ShouldHaveValidationErrorFor(x => x.Seats);
    }

    [Fact]
    public async Task Validate_WhenSeatsContainDuplicateWithDifferentRowCasing_HasValidationError() 
    {
        var validator = new CreateSeatsCommandValidator();
        var seats = new List<CreateSeatItem> { new("A", 10, 15m), new("a", 10, 15m) };
        var command = new CreateSeatsCommand(2, seats.AsReadOnly());

        var results = await validator.TestValidateAsync(command);

        results.ShouldHaveValidationErrorFor(x => x.Seats);
    }

    [Fact]
    public async Task Validate_WhenSeatsContainDuplicateWithWhitespace_HasValidationError() 
    {
        var validator = new CreateSeatsCommandValidator();
        var seats = new List<CreateSeatItem> { new("  A", 10, 15m), new("  a  ", 10, 15m) };
        var command = new CreateSeatsCommand(2, seats.AsReadOnly());

        var results = await validator.TestValidateAsync(command);

        results.ShouldHaveValidationErrorFor(x => x.Seats);
    }

    [Fact]
    public async Task Validate_WhenRowsMatchButNumbersDiffer_HasNoDuplicateValidationError() 
    {
        var validator = new CreateSeatsCommandValidator();
        var seats = new List<CreateSeatItem> { new("A", 11, 15m), new("A", 10, 15m) };
        var command = new CreateSeatsCommand(2, seats.AsReadOnly());

        var results = await validator.TestValidateAsync(command);

        results.ShouldNotHaveValidationErrorFor(x => x.Seats);
    }

    [Fact]
    public async Task Validate_WhenAllIsValid_HasNoValidationError()
    {
        var validator = new CreateSeatsCommandValidator();
        var seats = new List<CreateSeatItem> { new("A", 3, 10m) };
        var command = new CreateSeatsCommand(2, seats.AsReadOnly());

        var results = await validator.TestValidateAsync(command);

        Assert.True(results.IsValid);
    }
}
