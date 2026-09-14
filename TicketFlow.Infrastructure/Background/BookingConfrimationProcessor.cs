using Microsoft.Extensions.Logging;
using TicketFlow.Application.Background;

namespace TicketFlow.Infrastructure.Background;

public class BookingConfrimationProcessor(ILogger<BookingConfrimationProcessor> logger) : IBookingConfirmationProcessor
{
    public async Task ProcessAsync(BookingConfirmationWork work, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing booking confirmation for Booking {BookingId}, User {UserId}", work.BookingId, work.UserId);
        
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

        logger.LogInformation("Booking confirmation processed for Booking {BookingId}", work.BookingId);
    }
}
