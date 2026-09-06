namespace TicketFlow.Application.Bookings.Exceptions;

public class SeatAlreadyBookedException(int seatId) : Exception($"Seat with ID {seatId} is already booked.")
{
    public int SeatId { get; set; } = seatId;
}
