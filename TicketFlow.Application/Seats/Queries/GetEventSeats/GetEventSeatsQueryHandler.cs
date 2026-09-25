using MediatR;
using TicketFlow.Application.Abstractions.Repositories;

namespace TicketFlow.Application.Seats.Queries.GetEventSeats;

public class GetEventSeatsQueryHandler(ISeatsRepository seatsRepository) : IRequestHandler<GetEventSeatsQuery, IEnumerable<SeatResult>>
{
    public async Task<IEnumerable<SeatResult>> Handle(GetEventSeatsQuery request, CancellationToken cancellationToken)
    {
        var seats = await seatsRepository.GetSeatsAsync(request.EventId, cancellationToken);

        return seats.Select(seat => new SeatResult(seat));
    }
}
