using MediatR;

namespace TicketFlow.Application.Seats.Queries.GetEventSeats;

public sealed record GetEventSeatsQuery(int EventId) : IRequest<IEnumerable<SeatResult>>;
