using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Background;
using TicketFlow.Application.Bookings.Commands.CreateBooking;
using TicketFlow.Application.Bookings.Interfaces;
using TicketFlow.Application.Bookings.Models;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Bookings.Commands.CreateBooking;

public class CreateBookingCommandHandlerTests
{
    private readonly Mock<ILogger<CreateBookingCommandHandler>> _logger;
    private readonly Mock<IBackgroundTaskQueue> _backgroundTaskQueue;
    private readonly Mock<IBookingRepository> _repository;
    private readonly CreateBookingCommandHandler _commandHandler;

    public CreateBookingCommandHandlerTests()
    {
        _logger = new Mock<ILogger<CreateBookingCommandHandler>>();
        _backgroundTaskQueue = new Mock<IBackgroundTaskQueue>();
        _repository = new Mock<IBookingRepository>();

        _commandHandler = new CreateBookingCommandHandler(_repository.Object, _backgroundTaskQueue.Object, _logger.Object);
    }


    [Fact]
    public async Task Handle_WhenValid_ReturnsCreated()
    {
        var seatId = 1;
        var userId = Guid.NewGuid().ToString();
        var seat = new Seat { Id = seatId, Event = new Event { EventDate = DateTime.UtcNow.AddDays(10) } };
        Booking? createdBooking = null;

        _repository
            .Setup(x => x.SaveChangesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repository
            .Setup(x => x.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Callback<Booking, CancellationToken>((booking, _) => createdBooking = booking)
            .Returns(Task.CompletedTask);

        _repository
            .Setup(x => x.GetSeatForBookingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        _backgroundTaskQueue
            .Setup(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var result = await _commandHandler.Handle(new CreateBookingCommand(1, seatId, userId), CancellationToken.None);

        _repository.Verify(x => x.GetSeatForBookingAsync(seatId, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(x => x.AddAsync(It.Is<Booking>(b => b.UserId == userId && b.SeatId == seatId), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(x => x.SaveChangesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        _backgroundTaskQueue.Verify(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), CancellationToken.None), Times.Once);

        Assert.IsType<BookingCreated>(result);
        Assert.NotNull(createdBooking);
        Assert.Equal(userId, createdBooking.UserId);
        Assert.Equal(seatId, createdBooking.SeatId);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepository()
    {
        var seatId = 1;
        var userId = Guid.NewGuid().ToString();
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var seat = new Seat
        {
            Id = seatId,
            Event = new Event { EventDate = DateTime.UtcNow.AddDays(10) }
        };

        _repository
            .Setup(x => x.GetSeatForBookingAsync(seatId, cancellationToken))
            .ReturnsAsync(seat);

        _repository
            .Setup(x => x.AddAsync(It.IsAny<Booking>(), cancellationToken))
            .Returns(Task.CompletedTask);

        _repository
            .Setup(x => x.SaveChangesAsync(It.IsAny<int>(), cancellationToken))
            .Returns(Task.CompletedTask);

        _backgroundTaskQueue
            .Setup(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        await _commandHandler.Handle(new CreateBookingCommand(1, seatId, userId), cancellationToken);

        _repository.Verify(x => x.GetSeatForBookingAsync(seatId, cancellationToken), Times.Once);

        _repository.Verify(x => x.AddAsync(It.IsAny<Booking>(), cancellationToken), Times.Once);

        _repository.Verify(x => x.SaveChangesAsync(It.IsAny<int>(), cancellationToken), Times.Once);

        _backgroundTaskQueue.Verify(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_PropagatesException()
    {
        var seatId = 1;
        var userId = Guid.NewGuid().ToString();

        var seat = new Seat
        {
            Id = seatId,
            Event = new Event { EventDate = DateTime.UtcNow.AddDays(10) }
        };

        _repository
            .Setup(x => x.GetSeatForBookingAsync(seatId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        var exception = new InvalidOperationException("Database unavailable");

        _repository
            .Setup(x => x.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _commandHandler.Handle(new CreateBookingCommand(1, seatId, userId), CancellationToken.None));

        _backgroundTaskQueue.Verify(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.Equal("Database unavailable", result.Message);
    }

    [Fact]
    public async Task Handle_WhenSeatNotFound_ReturnsSeatNotFound()
    {
        var seatId = 5;
        var eventId = 1;
        var userId = Guid.NewGuid().ToString();

        _repository
            .Setup(x => x.GetSeatForBookingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Seat?)null);

        var result = await _commandHandler.Handle(new CreateBookingCommand(eventId, seatId, userId), CancellationToken.None);
        _repository.Verify(x => x.AddAsync(It.Is<Booking>(b => b.UserId == userId && b.SeatId == seatId), It.IsAny<CancellationToken>()), Times.Never);
        _backgroundTaskQueue.Verify(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.IsType<BookingSeatNotFound>(result);
    }

    [Fact]
    public async Task Handle_WhenSeatAlreadyBooked_ReturnsAlreadyBooked()
    {
        var seat = new Seat { Id = 1, Booking = new Booking { Id = 2 }, Event = new Event { EventDate = DateTime.UtcNow.AddDays(10) } };
        var userId = Guid.NewGuid().ToString();

        _repository
            .Setup(x => x.GetSeatForBookingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        var result = await _commandHandler.Handle(new CreateBookingCommand(1, seat.Id, userId), CancellationToken.None);

        _repository.Verify(x => x.AddAsync(It.Is<Booking>(b => b.UserId == userId && b.SeatId == seat.Id), It.IsAny<CancellationToken>()), Times.Never);
        _backgroundTaskQueue.Verify(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.IsType<BookingSeatAlreadyBooked>(result);
    }

    [Fact]
    public async Task Handle_WhenEventUnavailable_ReturnsEventUnavailable()
    {
        var seat = new Seat { Id = 1, Event = new Event { EventDate = DateTime.UtcNow.AddDays(-10) } };
        var userId = Guid.NewGuid().ToString();

        _repository
            .Setup(x => x.GetSeatForBookingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        var result = await _commandHandler.Handle(new CreateBookingCommand(1, seat.Id, userId), CancellationToken.None);

        _repository.Verify(x => x.AddAsync(It.Is<Booking>(b => b.UserId == userId && b.SeatId == seat.Id), It.IsAny<CancellationToken>()), Times.Never);
        _backgroundTaskQueue.Verify(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.IsType<BookingEventUnavailable>(result);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesThrowsError_BackgroundTaskNotQueued()
    {
        var seatId = 1;
        var seat = new Seat { Id = seatId, Event = new Event { EventDate = DateTime.UtcNow.AddDays(10) } };
        Booking? createdBooking = null;
        var message = "something went wrong while persisting the booking";
        var exception = new InvalidOperationException(message);
        var userId = Guid.NewGuid().ToString();

        _repository
            .Setup(x => x.SaveChangesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        _repository
            .Setup(x => x.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Callback<Booking, CancellationToken>((booking, _) => createdBooking = booking)
            .Returns(Task.CompletedTask);

        _repository
            .Setup(x => x.GetSeatForBookingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        _backgroundTaskQueue
            .Setup(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() => _commandHandler.Handle(new CreateBookingCommand(1, seatId, userId), CancellationToken.None));
        Assert.Equal(message, result.Message);

        _repository.Verify(x => x.AddAsync(It.Is<Booking>(b => b.UserId == userId && b.SeatId == seatId), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(x => x.SaveChangesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        _backgroundTaskQueue.Verify(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), CancellationToken.None), Times.Never);
    }
}
