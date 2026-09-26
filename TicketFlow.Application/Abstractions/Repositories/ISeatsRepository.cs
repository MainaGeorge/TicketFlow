using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Abstractions.Repositories;

public interface ISeatsRepository
{
    Task<Seat?> GetSeatAsync(int eventId, int seatId, CancellationToken cancellationToken);
    Task<IEnumerable<Seat>> GetSeatsAsync(int eventId, CancellationToken cancellationToken);
    Task<IEnumerable<Seat>> CreateSeatsAsync(IEnumerable<Seat> seats, CancellationToken cancellationToken);
}
