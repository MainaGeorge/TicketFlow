using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Background;
using TicketFlow.Contracts.IntegrationEvents;
using TicketFlow.Infrastructure.Messaging.Consumers;

namespace TicketFlow.Tests.Infrastructure.Messaging.Consumers;

public class BookingConfirmationConsumerTests
{
    [Fact]
    public async Task Consume_WhenBookingCreatedIntegrationEventIsReceived_ProcessesBookingConfirmation()
    {
        var logger = new Mock<ILogger<BookingConfirmationConsumer>>();
        var processor = new Mock<IBookingConfirmationProcessor>();
        var bookingId = 1;
        var userId = "userId";
        using var source = new CancellationTokenSource();
        var token = source.Token;

        var message = new BookingCreatedIntegrationEvent(bookingId, userId);

        processor
            .Setup(x => x.ProcessAsync(It.IsAny<BookingConfirmationWork>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var consumer = new BookingConfirmationConsumer(processor.Object, logger.Object);
        var context = new Mock<ConsumeContext<BookingCreatedIntegrationEvent>>();

        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken).Returns(token);

        await consumer.Consume(context.Object);

        processor.Verify(x => x.ProcessAsync(It.Is<BookingConfirmationWork>(w => w.BookingId == bookingId && w.UserId == userId), token), Times.Once);
    }
}
