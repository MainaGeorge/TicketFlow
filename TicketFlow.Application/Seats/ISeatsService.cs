using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Seats;

public interface ISeatsService
{
    Task<SeatBaseResult?> GetSeatAsync(int eventId, int seatId, CancellationToken cancellationToken);
    Task<IEnumerable<SeatBaseResult>> GetSeatsAsync(int eventId, CancellationToken cancellation);
    Task<SeatBaseResult> CreateSeatAsync(int eventId, Seat seat, CancellationToken cancellationToken);
}
