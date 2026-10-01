using FluentValidation;
using MediatR;

namespace TicketFlow.Application.Common.Behaviours;

public class ValidationBehaviour<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var validationContext = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(validators.Select(validator => validator.ValidateAsync(validationContext, cancellationToken)));
        var failures = validationResults.SelectMany(v => v.Errors).ToList();

        if(failures.Count > 0)
            throw new ValidationException(failures);

        return await next(cancellationToken);
    }
}