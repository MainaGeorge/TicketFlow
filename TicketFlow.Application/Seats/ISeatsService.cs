using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Seats;

public interface ISeatsService
{
    Task<SeatBaseResult?> GetSeatAsync(int eventId, int seatId);
    Task<IEnumerable<SeatBaseResult>> GetSeatsAsync(int eventId);
    Task<SeatBaseResult> CreateSeatAsync(int eventId, Seat seat);
}
