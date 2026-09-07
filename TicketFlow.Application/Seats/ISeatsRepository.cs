using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Seats;

public interface ISeatsRepository
{
    Task<Seat?> GetSeatAsync(int eventId, int seatId);
    Task<IEnumerable<Seat>> GetSeatsAsync(int eventId);
    Task<Seat> CreateSeatAsync(Seat seat);
}
