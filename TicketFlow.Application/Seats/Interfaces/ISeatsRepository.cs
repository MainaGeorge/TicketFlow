using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Seats.Interfaces;

public interface ISeatsRepository
{
    Task<Seat?> GetSeatAsync(int eventId, int seatId, CancellationToken cancellationToken);
    Task<IEnumerable<Seat>> GetSeatsAsync(int eventId, CancellationToken cancellationToken);
    Task<Seat> CreateSeatAsync(Seat seat, CancellationToken cancellationToken);
}
