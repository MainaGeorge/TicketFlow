using MediatR;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Abstractions.Repositories;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Seats.Commands;

public class CreateSeatCommandHandler(
    ISeatsRepository seatsRepository,
    IEventsRepository eventRepository,
    ILogger<CreateSeatCommandHandler> logger) 
    : IRequestHandler<CreateSeatCommand, SeatBaseResult>
{
    public async Task<SeatBaseResult> Handle(CreateSeatCommand request, CancellationToken cancellationToken)
    {
        var @event = await eventRepository.GetEventAsync(request.EventId, cancellationToken);

        if (@event is null)
        {
            logger.LogWarning("Attempted to create a seat for a non-existent event. EventId: {EventId}", request.EventId);
            return new EventNotFoundForSeatResult();
        }

        var seat = new Seat { EventId = request.EventId, Number = request.Number, Price = request.Price, Row = request.Row };
        var newSeat = await seatsRepository.CreateSeatAsync(seat, cancellationToken);

        return new SeatCreatedResult(newSeat);
    }
}
