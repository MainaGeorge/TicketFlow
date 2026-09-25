using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Abstractions.Repositories;

public interface ISeatsRepository
{
    Task<Seat?> GetSeatAsync(int eventId, int seatId, CancellationToken cancellationToken);
    Task<IEnumerable<Seat>> GetSeatsAsync(int eventId, CancellationToken cancellationToken);
    Task<Seat> CreateSeatAsync(Seat seat, CancellationToken cancellationToken);
}
