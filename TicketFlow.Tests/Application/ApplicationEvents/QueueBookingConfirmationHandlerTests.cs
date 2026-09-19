using Moq;
using System.Text.Json;
using TicketFlow.Application.ApplicationEvents.BookingCreated;
using TicketFlow.Application.Background;

namespace TicketFlow.Tests.Application.ApplicationEvents;

public class QueueBookingConfirmationHandlerTests
{
    private readonly Mock<IBackgroundTaskQueue> _queue;
    private readonly QueueBookingConfirmationHandler _handler;

    public QueueBookingConfirmationHandlerTests()
    {
        _queue = new Mock<IBackgroundTaskQueue>();
        _handler = new QueueBookingConfirmationHandler(_queue.Object);
    }

    [Fact]
    public async Task Handle_WhenBookingCreated_QueuesBookingConfirmationWork()
    {
        BackgroundWorkItem capturedWorkItem = null;

        _queue
            .Setup(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()))
            .Callback((BackgroundWorkItem w, CancellationToken c) => capturedWorkItem = w)
            .Returns(ValueTask.CompletedTask);

        var notification = new BookingCreatedEvent(123, "user-1");

        await _handler.Handle(notification, CancellationToken.None);

        Assert.NotNull(capturedWorkItem);

        Assert.Equal(nameof(BookingConfirmationWork), capturedWorkItem.JobType);

        var payload = JsonSerializer.Deserialize<BookingConfirmationWork>(capturedWorkItem.Payload);

        Assert.NotNull(payload);
        Assert.Equal(123, payload.BookingId);
        Assert.Equal("user-1", payload.UserId);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToQueue()
    {
        using var source = new CancellationTokenSource();
        var token = source.Token;

        _queue
            .Setup(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), token))
            .Returns(ValueTask.CompletedTask);

        await _handler.Handle(new BookingCreatedEvent(1, "user-id"), token);

        _queue.Verify(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), token), Times.Once);
    }
}
