using MediatR;

namespace TicketFlow.Application.Seats.Commands;

public sealed record CreateSeatCommand(int EventId, string Row, int Number, decimal Price) : IRequest<SeatBaseResult>;