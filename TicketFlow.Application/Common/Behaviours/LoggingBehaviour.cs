using MediatR;
using Microsoft.Extensions.Logging;

namespace TicketFlow.Application.Common.Behaviours;

public class LoggingBehaviour<TRequest, TResponse>(
    ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;

        logger.LogInformation("About to start handling {RequestName}", name);

        var response = await next(cancellationToken);

        logger.LogInformation("Finished handling {RequestName}", name);

        return response;
    }
}
