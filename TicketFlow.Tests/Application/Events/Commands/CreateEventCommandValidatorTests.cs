using TicketFlow.Application.Events.Commands.CreateEvent;
using FluentValidation.TestHelper;

namespace TicketFlow.Tests.Application.Events.Commands;

public class CreateEventCommandValidatorTests
{
    [Fact]
    public async Task Validate_WhenDateIsDefault_HasValidationError()
    {
        var validator = new CreateEventCommandValidator();

        var command = new CreateEventCommand("Test Event", "Test Venue", default, "user-id");

        var result = await validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Date);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("    ")]
    public async Task Validate_WhenVenueIsInvalid_HasValidationError(string? venue)
    {
        var validator = new CreateEventCommandValidator();

        var command = new CreateEventCommand("Test Event", venue, DateTime.UtcNow.AddDays(1), "user-id");
        var result = await validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Venue);
    }

    [Fact]
    public async Task Validate_WhenVenueIsMoreThan200Characters_HasValidationError()
    {
        var venue = new string('v', 201);
        var validator = new CreateEventCommandValidator();

        var command = new CreateEventCommand("Test Name", venue, DateTime.UtcNow.AddDays(1), "user-id");
        var result = await validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Venue);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("    ")]
    public async Task Validate_WhenNameIsInvalid_HasValidationError(string? name)
    {
        var validator = new CreateEventCommandValidator();

        var command = new CreateEventCommand(name, "Test Venue", DateTime.UtcNow.AddDays(1), "user-id");
        var result = await validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task Validate_WhenNameIsMoreThan200Characters_HasValidationError()
    {
        var name = new string('q', 201);
        var validator = new CreateEventCommandValidator();

        var command = new CreateEventCommand(name, "Test Venue", DateTime.UtcNow.AddDays(1), "user-id");
        var result = await validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task Validate_WhenAllIsValid_HasNoValidationError()
    {
        var validator = new CreateEventCommandValidator();

        var command = new CreateEventCommand("Test Event", "Test Venue", DateTime.UtcNow.AddDays(1), "user-id");

        var result = await validator.TestValidateAsync(command);

        Assert.True(result.IsValid);
    }
}
