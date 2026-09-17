using MediatR;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Events.Interfaces;
using TicketFlow.Application.Events.Models;

namespace TicketFlow.Application.Events.Queries.GetEvent;

public class GetEventQueryHandler(
    IEventsRepository eventsRepository,
    ILogger<GetEventQueryHandler> logger) 
    : IRequestHandler<GetEventQuery, EventBaseResult>
{
    public async Task<EventBaseResult> Handle(GetEventQuery query, CancellationToken cancellationToken)
    {
        var @event = await eventsRepository.GetEventAsync(query.Id, cancellationToken);

        if (@event is null)
        {
            logger.LogWarning("Event not found. EventId: {EventId}", query.Id);
            return new EventNotFoundResult();
        }

        return new EventResult(@event);
    }
}
