using MediatR;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Seats.Interfaces;

namespace TicketFlow.Application.Seats.Queries.GetSeat;

public class GetSeatQueryHandler(ISeatsRepository seatsRepository, ILogger<GetSeatQueryHandler> logger) : IRequestHandler<GetSeatQuery, SeatBaseResult>
{
    public async Task<SeatBaseResult> Handle(GetSeatQuery request, CancellationToken cancellationToken)
    {
        var seat = await seatsRepository.GetSeatAsync(request.EventId, request.SeatId, cancellationToken);

        if (seat is null)
        {
            logger.LogWarning("Seat {seatId} for event {eventId} not found", request.SeatId, request.EventId);
            return new SeatNotFound();
        }

        return new SeatResult(seat);
    }
}
