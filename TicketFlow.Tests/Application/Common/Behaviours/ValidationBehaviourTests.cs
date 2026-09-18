using FluentValidation;
using MediatR;
using TicketFlow.Application.Common.Behaviours;

namespace TicketFlow.Tests.Application.Common.Behaviours;



public class ValidationBehaviourTests
{
    [Fact]
    public async Task Handle_WhenValidationSucceeds_CallsNextAndReturnsResponse()
    {
        var count = 0;
        var validators = new List<IValidator<TestRequest>> { new TestRequestValidator() };
        var response = new TestResponse("Test Response");
        var validationBehaviour = new ValidationBehaviour<TestRequest, TestResponse>(validators);
        var request = new TestRequest("Test Request");

        RequestHandlerDelegate<TestResponse> next = (cancellationToken) =>
        {
            count++;
            return Task.FromResult(response);
        };

        var result = await validationBehaviour.Handle(request, next, CancellationToken.None);

        Assert.Same(response, result);
        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Handle_WhenValidationFails_ThrowsValidationExceptionAndDoesNotCallNext(string testRequestValue)
    {
        var count = 0;
        var validators = new List<IValidator<TestRequest>> { new TestRequestValidator() };
        var response = new TestResponse("This should never be called");
        var validationBehaviour = new ValidationBehaviour<TestRequest, TestResponse>(validators);
        var request = new TestRequest(testRequestValue);

        RequestHandlerDelegate<TestResponse> next = (cancellationToken) =>
        {
            count++;
            return Task.FromResult(response);
        };

        var exception = await Assert.ThrowsAsync<ValidationException>(() => validationBehaviour.Handle(request, next, CancellationToken.None));


        Assert
            .Contains(
                exception.Errors,
                error => error.PropertyName == nameof(TestRequest.Name)
            );
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Handle_WhenNoValidatorsExist_CallsNextAndReturnsResponse()
    {
        var count = 0;
        var validators = new List<IValidator<TestRequest>>();
        var response = new TestResponse("Test Response");
        var validationBehaviour = new ValidationBehaviour<TestRequest, TestResponse>(validators);
        var request = new TestRequest("Test Request");

        RequestHandlerDelegate<TestResponse> next = (cancellationToken) =>
        {
            count++;
            return Task.FromResult(response);
        };

        var result = await validationBehaviour.Handle(request, next, CancellationToken.None);

        Assert.Same(response, result);
        Assert.Equal(1, count);
    }

}

public sealed class TestRequestValidator : AbstractValidator<TestRequest>
{
    public TestRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

public sealed record TestRequest(string Name);
public sealed record TestResponse(string Value);
