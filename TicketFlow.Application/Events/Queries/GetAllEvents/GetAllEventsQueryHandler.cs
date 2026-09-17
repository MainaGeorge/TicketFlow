using MediatR;
using TicketFlow.Application.Events.Interfaces;
using TicketFlow.Application.Events.Models;

namespace TicketFlow.Application.Events.Queries.GetAllEvents;

public class GetAllEventsQueryHandler(IEventsRepository eventsRepository) : IRequestHandler<GetAllEventsQuery, IEnumerable<EventResult>>
{
    public async Task<IEnumerable<EventResult>> Handle(GetAllEventsQuery request, CancellationToken cancellationToken)
    {
        var events = await eventsRepository
            .GetAllEventsAsync(cancellationToken);

        return events.Select(e => new EventResult(e));
    }
}
