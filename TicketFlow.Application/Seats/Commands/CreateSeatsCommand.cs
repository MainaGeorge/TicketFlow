using MediatR;
using TicketFlow.Application.Seats.Models;

namespace TicketFlow.Application.Seats.Commands;

public sealed record CreateSeatsCommand(int EventId, IReadOnlyCollection<CreateSeatItem> Seats) : IRequest<SeatBaseResult>;