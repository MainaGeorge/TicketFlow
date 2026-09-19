using Moq;
using TicketFlow.Application.Bookings.Interfaces;
using TicketFlow.Application.Bookings.Models;
using TicketFlow.Application.Bookings.Queries.GetBooking;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Bookings.Queries.GetBooking;

public class GetBookingQueryHandlerTests
{
    private readonly Mock<IBookingRepository> _repository;
    private readonly GetBookingQueryHandler _commandHandler;

    public GetBookingQueryHandlerTests()
    {
        _repository = new Mock<IBookingRepository>();
        _commandHandler = new GetBookingQueryHandler(_repository.Object);
    }

    [Fact]
    public async Task Handle_WhenBookingExists_ReturnsBooking()
    {
        var bookingId = 1;
        var userId = Guid.NewGuid().ToString();
        var bookingCommand = new GetBookingQuery(bookingId, userId);
        var bookingResult = new BookingResult(bookingId, 1, userId, DateTime.UtcNow, "paymentReference", "A", 10, 100m, 10);

        _repository
            .Setup(x => x.GetBookingAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookingResult);

        var booking = await _commandHandler.Handle(bookingCommand, CancellationToken.None);

        var result = Assert.IsType<BookingResult>(booking);

        _repository.Verify(x => x.GetBookingAsync(bookingId, userId, CancellationToken.None), Times.Once);

        Assert.Equal(bookingId, result.Id);
    }

    [Fact]
    public async Task Handle_WhenBookingDoesNotExist_ReturnsNoBooking()
    {
        var bookingId = 1;
        var userId = Guid.NewGuid().ToString();
        var bookingCommand = new GetBookingQuery(bookingId, userId);

        _repository
            .Setup(x => x.GetBookingAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BookingResult?)null);

        var booking = await _commandHandler.Handle(bookingCommand, CancellationToken.None);
        _repository.Verify(x => x.GetBookingAsync(bookingId, userId, It.IsAny<CancellationToken>()), Times.Once);

        var result = Assert.IsType<BookingNotFound>(booking);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepository()
    {
        var bookingId = 1;
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var userId = Guid.NewGuid().ToString();
        var bookingCommand = new GetBookingQuery(bookingId, userId);
        var bookingResult = new BookingResult(bookingId, 1, userId, DateTime.UtcNow, "paymentReference", "A", 10, 100m, 10);

        _repository
            .Setup(x => x.GetBookingAsync(bookingId, userId, cancellationToken))
            .ReturnsAsync(bookingResult);

        await _commandHandler.Handle(bookingCommand, cancellationToken);

        _repository.Verify(x => x.GetBookingAsync(bookingId, userId, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCancellationRequested_PassesCancelledToken()
    {
        using var cts = new CancellationTokenSource();
        var userId = Guid.NewGuid().ToString();
        var bookingId = 1;
        var command = new GetBookingQuery(bookingId, userId);
        cts.Cancel();

        _repository
            .Setup(x => x.GetBookingAsync(It.IsAny<int>(), It.IsAny<string>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() => _commandHandler.Handle(new GetBookingQuery(1, userId), cts.Token));
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_PropagatesException()
    {
        var exception = new InvalidOperationException("Unexpected failure");
        var bookingId = 1;
        var userId = Guid.NewGuid().ToString();
        var bookingCommand = new GetBookingQuery(bookingId, userId);

        _repository
            .Setup(x => x.GetBookingAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _commandHandler.Handle(bookingCommand, CancellationToken.None));

        Assert.Equal("Unexpected failure", result.Message);
    }
}
