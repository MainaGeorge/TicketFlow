using MediatR;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Events.Interfaces;
using TicketFlow.Application.Events.Models;

namespace TicketFlow.Application.Events.Queries.GetEvent;

public class GetEventQueryHandler(
    IEventsRepository eventsRepository,
    ICacheService cacheService,
    ILogger<GetEventQueryHandler> logger)
    : IRequestHandler<GetEventQuery, EventBaseResult>
{
    public async Task<EventBaseResult> Handle(GetEventQuery query, CancellationToken cancellationToken)
    {
        var cacheKey = $"event:{query.Id}";
        var cacheReadSucceeded = false;
        EventResult? cachedEvent = null;

        try
        {
            cachedEvent = await cacheService.GetAsync<EventResult>(cacheKey, cancellationToken);
            cacheReadSucceeded = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to read Event {EventId} from cache.", query.Id);
        }

        if (cachedEvent is not null)
            return cachedEvent;

        var @event = await eventsRepository.GetEventAsync(query.Id, cancellationToken);

        if (@event is null)
        {
            logger.LogWarning("Event not found. EventId: {EventId}", query.Id);
            return new EventNotFoundResult();
        }

        var eventResult = new EventResult(@event);

        if (!cacheReadSucceeded)
            return eventResult;

        try
        {
            await cacheService.SetAsync(cacheKey, eventResult, TimeSpan.FromMinutes(5), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to write Event {EventId} to cache.", query.Id);
        }

        return eventResult;
    }
}
