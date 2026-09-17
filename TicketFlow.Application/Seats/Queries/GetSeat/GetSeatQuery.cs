using MediatR;

namespace TicketFlow.Application.Seats.Queries.GetSeat;

public sealed record GetSeatQuery(int EventId, int SeatId) : IRequest<SeatBaseResult>;

