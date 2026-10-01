using MassTransit;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Background;
using TicketFlow.Contracts.IntegrationEvents;

namespace TicketFlow.Infrastructure.Messaging.Consumers;

public sealed class BookingConfirmationConsumer(
    IBookingConfirmationProcessor processor,
    ILogger<BookingConfirmationConsumer> logger) : IConsumer<BookingCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<BookingCreatedIntegrationEvent> context)
    {
        logger.LogInformation("Received booking created integration event for booking {BookingId} and user {UserId}", context.Message.BookingId, context.Message.UserId);

        var work = new BookingConfirmationWork(context.Message.BookingId, context.Message.UserId);

        await processor.ProcessAsync(work, context.CancellationToken);
    }
}
