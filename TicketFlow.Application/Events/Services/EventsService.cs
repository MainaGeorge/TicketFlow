using Microsoft.Extensions.Logging;
using TicketFlow.Application.Events.Interfaces;
using TicketFlow.Application.Events.Models;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Events.Services;

public class EventsService(IEventsRepository eventRepository, ILogger<EventsService> logger) : IEventsService
{
    public async Task<EventBaseResult> CreateEventAsync(Event @event, string userId, CancellationToken cancellationToken = default)
    {
        if (@event.EventDate <= DateTime.UtcNow)
        {
            logger.LogWarning("User {userId} attempted to create a past event: {EventId}, Name: {EventName}, Venue: {EventVenue}, Date: {EventDate}", userId, @event.Id, @event.Name, @event.Venue, @event.EventDate);
            return new PastEventResult(@event);
        }

        var createdEvent = await eventRepository.CreateEventAsync(@event, cancellationToken);

        logger.LogInformation("Event created successfully: {EventId}, Name: {EventName}, Venue: {EventVenue}, Date: {EventDate}", @event.Id, @event.Name, @event.Venue, @event.EventDate);

        return new EventCreatedResult(createdEvent);
    }

    public async Task<IEnumerable<EventResult>> GetAllEventsAsync(CancellationToken cancellationToken = default)
    {
       var events = await eventRepository
            .GetAllEventsAsync(cancellationToken);

        return events.Select(e => new EventResult(e));
    }

    public async Task<EventBaseResult?> GetEventAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var @event = await eventRepository.GetEventAsync(eventId, cancellationToken);

        if(@event is null)
        {
            logger.LogWarning("Event not found. EventId: {EventId}", eventId);
            return new EventNotFoundResult(null);
        }

        return new EventResult(@event);
    }
}
