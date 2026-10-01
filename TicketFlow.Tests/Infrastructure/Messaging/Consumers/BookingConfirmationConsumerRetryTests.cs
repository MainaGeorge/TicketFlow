using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TicketFlow.Application.Background;
using TicketFlow.Contracts.IntegrationEvents;
using TicketFlow.Infrastructure.Messaging.Consumers;

namespace TicketFlow.Tests.Infrastructure.Messaging.Consumers;

public class BookingConfirmationConsumerRetryTests
{
    [Fact]
    public async Task Consume_WhenProcessingFails_RetriesAccordingToConfiguredPolicy()
    {
        var processor = new Mock<IBookingConfirmationProcessor>();

        processor
            .Setup(x => x.ProcessAsync(It.IsAny<BookingConfirmationWork>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated failure"));

        var services = new ServiceCollection()
            .AddLogging()
            .AddScoped(_ => processor.Object)
            .AddMassTransitTestHarness(configurator =>
            {
                configurator.AddConsumer<BookingConfirmationConsumer>(consumer =>
                {
                    consumer.UseMessageRetry(retry =>
                    {
                        retry.Interval(retryCount: 3, interval: TimeSpan.FromMilliseconds(10));
                    });
                });
            });

        await using var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var bookingId = 1;
        var userId = "user-id";
        var message = new BookingCreatedIntegrationEvent(bookingId, userId);

        await harness.Bus.Publish(message);

        // wait on the harness to finish consuming the event
        await harness.Consumed.Any<BookingCreatedIntegrationEvent>();

        // since processing fails, the harness will set a fault for that after all retries have been exhausted
        var faulted = await harness.Published.Any<Fault<BookingCreatedIntegrationEvent>>();
        Assert.True(faulted);


        // we have configured 3 retries and 1 original try, so in total processor should be called four times
        processor
            .Verify(x => x.ProcessAsync(It.Is<BookingConfirmationWork>(work => work.BookingId == bookingId &&work.UserId == userId), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }
}
