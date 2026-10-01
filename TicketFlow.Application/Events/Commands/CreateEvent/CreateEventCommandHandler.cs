using MediatR;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Abstractions.Repositories;
using TicketFlow.Application.Events.Models;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Events.Commands.CreateEvent;

public class CreateEventCommandHandler(
    IEventsRepository eventRepository,
    ILogger<CreateEventCommandHandler> logger) : IRequestHandler<CreateEventCommand, EventBaseResult>
{
    public async Task<EventBaseResult> Handle(CreateEventCommand command, CancellationToken cancellationToken)
    {
        var @event = new Event { EventDate = command.Date, Name = command.Name, Venue = command.Venue};
        if (command.Date <= DateTime.UtcNow)
        {
            logger.LogWarning(
                "User {userId} attempted to create a past event, Name: {EventName}, Venue: {EventVenue}, Date: {EventDate}",
                command.UserId, command.Name, command.Venue, command.Date);

            return new PastEventResult(@event);
        }

        var createdEvent = await eventRepository.CreateEventAsync(@event, cancellationToken);

        logger.LogInformation(
            "Event created successfully: {EventId}, Name: {EventName}, Venue: {EventVenue}, Date: {EventDate}",
            @event.Id, @event.Name, @event.Venue, @event.EventDate);

        return new EventCreatedResult(createdEvent);
    }
}