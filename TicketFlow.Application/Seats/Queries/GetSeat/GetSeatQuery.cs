using MediatR;
using TicketFlow.Application.Seats.Models;

namespace TicketFlow.Application.Seats.Queries.GetSeat;

public sealed record GetSeatQuery(int EventId, int SeatId) : IRequest<SeatBaseResult>;

