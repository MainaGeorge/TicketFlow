using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Text.Json;
using TicketFlow.Application;
using TicketFlow.Application.ApplicationEvents.BookingCreated;
using TicketFlow.Application.Background;

namespace TicketFlow.Tests.Integration;

public class QueueBookingConfirmationTests
{
    [Fact]
    public async Task Publish_WhenBookingCreated_QueuesBookingConfirmationWork()
    {
        var queue = new Mock<IBackgroundTaskQueue>();
        BackgroundWorkItem? capturedWorkItem = null;

        queue
            .Setup(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()))
            .Callback<BackgroundWorkItem, CancellationToken>((workItem, _) => capturedWorkItem = workItem)
            .Returns(ValueTask.CompletedTask);

        var services = new ServiceCollection();

        services.AddLogging();

        var config = new ConfigurationBuilder().Build();

        services.AddApplication(config);

        services.AddSingleton(queue.Object);

        await using var provider = services.BuildServiceProvider();

        var publisher = provider.GetRequiredService<IPublisher>();

        var notification = new BookingCreatedEvent(123, "user-1");

        await publisher.Publish(notification);

        Assert.NotNull(capturedWorkItem);

        Assert.Equal(nameof(BookingConfirmationWork), capturedWorkItem.JobType);

        var payload = JsonSerializer.Deserialize<BookingConfirmationWork>(capturedWorkItem.Payload);

        Assert.NotNull(payload);
        Assert.Equal(123, payload.BookingId);
        Assert.Equal("user-1", payload.UserId);
    }
}
