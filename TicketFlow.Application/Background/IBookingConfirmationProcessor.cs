namespace TicketFlow.Application.Background;

public interface IBookingConfirmationProcessor
{
    Task ProcessAsync(BookingConfirmationWork work, CancellationToken cancellationToken = default);
}
