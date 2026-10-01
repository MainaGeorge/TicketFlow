using MediatR;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Abstractions.Repositories;
using TicketFlow.Application.Seats.Exceptions;
using TicketFlow.Application.Seats.Models;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Seats.Commands;

public class CreateSeatsCommandHandler(
    ISeatsRepository seatsRepository,
    IEventsRepository eventRepository,
    ILogger<CreateSeatsCommandHandler> logger) 
    : IRequestHandler<CreateSeatsCommand, SeatBaseResult>
{
    public async Task<SeatBaseResult> Handle(CreateSeatsCommand request, CancellationToken cancellationToken)
    {
        var @event = await eventRepository.GetEventAsync(request.EventId, cancellationToken);

        if (@event is null)
        {
            logger.LogWarning("Attempted to create a seat for a non-existent event. EventId: {EventId}", request.EventId);
            return new EventNotFoundForSeatResult();
        }

        var seats = request.Seats
            .Select(x => new Seat { Row = x.Row.Trim().ToUpperInvariant(), Number = x.Number, Price = x.Price, EventId = request.EventId })
            .ToList();

        try
        {
            await seatsRepository.CreateSeatsAsync(seats, cancellationToken);
            return new SeatsCreatedResult(seats);
        }
        catch (DuplicateSeatException)
        {
            logger.LogWarning("Duplicate seat detected during seat creation for {EventId}", request.EventId);
            return new DuplicateSeatResult();
        }
        
    }
}
