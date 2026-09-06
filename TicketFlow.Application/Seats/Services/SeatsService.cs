using Microsoft.Extensions.Logging;
using TicketFlow.Application.Events.Interfaces;
using TicketFlow.Application.Seats.Interfaces;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Seats.Services;

public class SeatsService(ILogger<SeatsService> logger, ISeatsRepository seatsRepository, IEventsRepository eventRepository) : ISeatsService
{
    public async Task<SeatBaseResult> CreateSeatAsync(int eventId, Seat seat, CancellationToken cancellationToken = default)
    {
        var @event = await eventRepository.GetEventAsync(eventId, cancellationToken);

        if(@event is null)
        {
            logger.LogWarning("Attempt to crate a seat without and event: {EventId}", eventId);
            return new EventNotFoundForSeatResult();
        }

        var newSeat = await seatsRepository.CreateSeatAsync(seat, cancellationToken);

        return new SeatCreatedResult(newSeat);

    }

    public async Task<SeatBaseResult?> GetSeatAsync(int eventId, int seatId, CancellationToken cancellationToken = default)
    {
        var seat = await seatsRepository.GetSeatAsync(eventId, seatId, cancellationToken);

        if (seat is null)
        {
            logger.LogWarning("Seat {seatId} for event {eventId} not found", seatId, eventId);
            return new SeatNotFound();
        }

        return new SeatResult(seat);
    }

    public async Task<IEnumerable<SeatBaseResult>> GetSeatsAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var seats = await seatsRepository.GetSeatsAsync(eventId, cancellationToken);

        return seats.Select(seat => new SeatResult(seat));
    }
}
