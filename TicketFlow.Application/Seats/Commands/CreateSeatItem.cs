namespace TicketFlow.Application.Seats.Commands;

public sealed record CreateSeatItem(string Row, int Number, decimal Price);
