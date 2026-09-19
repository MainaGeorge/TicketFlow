using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using TicketFlow.Application.Background;

namespace TicketFlow.Application.ApplicationEvents.BookingCreated;

public class QueueBookingConfirmationHandler(IBackgroundTaskQueue backgroundTaskQueue)
    : INotificationHandler<BookingCreatedEvent>
{
    public async Task Handle(BookingCreatedEvent notification, CancellationToken cancellationToken)
    {
        var confirmationWork = new BookingConfirmationWork(notification.BookingId, notification.UserId);

        var payload = JsonSerializer.Serialize(confirmationWork);

        await backgroundTaskQueue.QueueAsync(
            new BackgroundWorkItem(
                JobType: nameof(BookingConfirmationWork),
                Payload: payload,
                ExecuteAsync: async (serviceProvider, ct) =>
                {
                    var processor = serviceProvider.GetRequiredService<IBookingConfirmationProcessor>();
                    await processor.ProcessAsync(confirmationWork, ct);
                }),
            cancellationToken);
    }
}
