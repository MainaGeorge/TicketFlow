using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Background;
using TicketFlow.Infrastructure.Background;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Tests.Integration;

namespace TicketFlow.Tests.Infrastructure.Background;

public class BookingConfirmationProcessorTests
{
    private readonly Mock<IProcessedJobStore> _backgroundProcessedJobStore;
    private readonly Mock<ILogger<BookingConfirmationProcessor>> _logger;
    private readonly BookingConfirmationProcessor _bookingConfrimationProcessor;

    public BookingConfirmationProcessorTests()
    {
        _backgroundProcessedJobStore = new Mock<IProcessedJobStore>();
        _logger = new Mock<ILogger<BookingConfirmationProcessor>>();
        _bookingConfrimationProcessor = new BookingConfirmationProcessor(_backgroundProcessedJobStore.Object, _logger.Object);
    }

    [Fact]
    public async Task BookingConfirmationProcessor_WhenBookingAlreadyProcessed_DoesNotReprocessItAgain()
    {
        var bookingConfirmationWork = new BookingConfirmationWork(2, "745");
        var idempotencyKey = $"BookingConfirmation:{bookingConfirmationWork.BookingId}";

        _backgroundProcessedJobStore
            .Setup(x => x.ExistsAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);


        await _bookingConfrimationProcessor.ProcessAsync(bookingConfirmationWork, CancellationToken.None);

        _backgroundProcessedJobStore.Verify(x => x.MarkProcessedAsync(idempotencyKey, CancellationToken.None), Times.Never);
        _backgroundProcessedJobStore.Verify(x => x.ExistsAsync(idempotencyKey, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task BookingConfirmationProcessor_WhenBookingHasNotBeenProcessed_ProcessesTheJob()
    {
        var bookingConfirmationWork = new BookingConfirmationWork(2, "745");
        var idempotencyKey = $"BookingConfirmation:{bookingConfirmationWork.BookingId}";


        _backgroundProcessedJobStore
            .Setup(x => x.ExistsAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _backgroundProcessedJobStore
            .Setup(x => x.MarkProcessedAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _bookingConfrimationProcessor.ProcessAsync(bookingConfirmationWork, CancellationToken.None);

        _backgroundProcessedJobStore.Verify(x => x.MarkProcessedAsync(idempotencyKey, CancellationToken.None), Times.Once);
        _backgroundProcessedJobStore.Verify(x => x.ExistsAsync(idempotencyKey, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task MarkProcessedAsync_WhenIdempotencyKeyAlreadyExists_ThrowsDbUpdateException()
    {
        await using var factory = new CustomWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var backgroundProcessedJobStore = new ProcessedJobStore(context);

        var idempotencyKey = $"BookingConfirmation:{Guid.NewGuid()}";

        await backgroundProcessedJobStore.MarkProcessedAsync(idempotencyKey, CancellationToken.None);

        await Assert.ThrowsAsync<DbUpdateException>(() => backgroundProcessedJobStore.MarkProcessedAsync(idempotencyKey, CancellationToken.None));
    }

    [Fact]
    public async Task MarkProcessedAsync_WhenIdempotencyKeyIsNew_PersistsJob()
    {
        var idempotencyKey = $"BookingConfirmation:{Guid.NewGuid()}";

        await using var factory = new CustomWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var backgroundProcessedJobStore = new ProcessedJobStore(context);

        await backgroundProcessedJobStore.MarkProcessedAsync(idempotencyKey, CancellationToken.None);
        var exists = await backgroundProcessedJobStore.ExistsAsync(idempotencyKey, CancellationToken.None);

        var processedJob = await context.ProcessedBackgroundJobs.SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey);

        Assert.True(exists);
        Assert.NotNull(processedJob);
        Assert.Equal(idempotencyKey, processedJob.IdempotencyKey);
    }

    [Fact]
    public async Task ProcessAsync_WhenBookingConfirmationWasAlreadyProcessed_DoesNotProcessAgain()
    {
        var processedJobStore = new Mock<IProcessedJobStore>();
        var logger = new Mock<ILogger<BookingConfirmationProcessor>>();

        var userId = "user-id";
        var bookingId = 1;
        var idempotencyKey = $"BookingConfirmation:{bookingId}";
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var bookingConfirmationWork = new BookingConfirmationWork(bookingId, userId);

        processedJobStore
            .Setup(x => x.ExistsAsync(idempotencyKey, cancellationToken))
            .ReturnsAsync(true);

        var bookingConfirmationProcessor = new BookingConfirmationProcessor(processedJobStore.Object, logger.Object);

        await bookingConfirmationProcessor.ProcessAsync(bookingConfirmationWork, cancellationToken);

        processedJobStore.Verify(x => x.ExistsAsync(idempotencyKey, cancellationToken), Times.Once);
        processedJobStore.Verify(x => x.MarkProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenBookingConfirmationHasNotBeenProcessed_MarksAsProcessed()
    {
        var processedJobStore = new Mock<IProcessedJobStore>();
        var logger = new Mock<ILogger<BookingConfirmationProcessor>>();

        var userId = "user-id";
        var bookingId = 1;
        var idempotencyKey = $"BookingConfirmation:{bookingId}";
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        var bookingConfirmationWork = new BookingConfirmationWork(bookingId, userId);

        processedJobStore
            .Setup(x => x.ExistsAsync(idempotencyKey, cancellationToken))
            .ReturnsAsync(false);

        processedJobStore
            .Setup(x => x.MarkProcessedAsync(idempotencyKey, cancellationToken))
            .Returns(Task.CompletedTask);

        var bookingConfirmationProcessor = new BookingConfirmationProcessor(processedJobStore.Object, logger.Object);

        await bookingConfirmationProcessor.ProcessAsync(bookingConfirmationWork, cancellationToken);

        processedJobStore.Verify(x => x.ExistsAsync(idempotencyKey, cancellationToken), Times.Once);
        processedJobStore.Verify(x => x.MarkProcessedAsync(idempotencyKey, cancellationToken), Times.Once);
    }
}
