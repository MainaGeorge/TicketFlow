namespace TicketFlow.Application.Bookings.Commands.CreateBooking;

public sealed record CreateBookingCommand(int EventId, int SeatId, string UserId);