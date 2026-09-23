using Microsoft.Extensions.Logging;
using TicketFlow.Application.Background;

namespace TicketFlow.Infrastructure.Background;

public class BookingConfirmationProcessor(
        IProcessedJobStore processedJobStore, 
        ILogger<BookingConfirmationProcessor> logger) 
    : IBookingConfirmationProcessor
{
    public async Task ProcessAsync(BookingConfirmationWork work, CancellationToken cancellationToken = default)
    {
        var idempotencyKey = $"BookingConfirmation:{work.BookingId}";

        if (await processedJobStore.ExistsAsync(idempotencyKey, cancellationToken))
        {
            logger.LogInformation("Booking confirmation for Booking {BookingId} has already been processed.", work.BookingId);
            return;
        }

        logger.LogInformation("Processing booking confirmation for Booking {BookingId}, User {UserId}", work.BookingId, work.UserId);

        //place holder for the work to be done
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

        await processedJobStore.MarkProcessedAsync(idempotencyKey, cancellationToken);

        logger.LogInformation("Booking confirmation processed for Booking {BookingId}", work.BookingId);
    }
}
