using MediatR;
using TicketFlow.Application.Seats.Models;

namespace TicketFlow.Application.Seats.Queries.GetEventSeats;

public sealed record GetEventSeatsQuery(int EventId) : IRequest<IEnumerable<SeatResult>>;
