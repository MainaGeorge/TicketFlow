using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using TicketFlow.Application.Events;
using TicketFlow.Domain.Entities;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TicketFlow.Application.Seats;

public class SeatsService(ILogger<SeatsService> logger, ISeatsRepository seatsRepository, IEventRepository eventRepository) : ISeatsService
{
    public async Task<SeatBaseResult> CreateSeatAsync(int eventId, Seat seat)
    {
        var @event = await eventRepository.GetEventAsync(eventId);

        if(@event is null)
        {
            logger.LogWarning("Attempt to crate a seat without and event: {EventId}", eventId);
            return new EventNotFoundForSeatResult();
        }

        var newSeat = await seatsRepository.CreateSeatAsync(seat);

        return new SeatCreatedResult(newSeat);

    }

    public async Task<SeatBaseResult?> GetSeatAsync(int eventId, int seatId)
    {
        var seat = await seatsRepository.GetSeatAsync(eventId, seatId);

        if (seat is null)
        {
            logger.LogWarning("Seat {seatId} for event {eventId} not found", seatId, eventId);
            return new SeatNotFound();
        }

        return new SeatResult(seat);
    }

    public async Task<IEnumerable<SeatBaseResult>> GetSeatsAsync(int eventId)
    {
        var seats = await seatsRepository.GetSeatsAsync(eventId);

        return seats.Select(seat => new SeatResult(seat));
    }
}
