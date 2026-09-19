using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Background;
using TicketFlow.Application.Bookings.Commands.CreateBooking;
using TicketFlow.Application.Bookings.Interfaces;
using TicketFlow.Application.Bookings.Models;
using TicketFlow.Application.Events.Interfaces;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Bookings.Commands.CreateBooking;

public class CreateBookingCommandHandlerTests
{
    private readonly Mock<ILogger<CreateBookingCommandHandler>> _logger;
    private readonly Mock<IBackgroundTaskQueue> _backgroundTaskQueue;
    private readonly Mock<IBookingRepository> _bookingRepository;
    private readonly Mock<IEventsRepository> _eventRepository;
    private readonly CreateBookingCommandHandler _commandHandler;

    public CreateBookingCommandHandlerTests()
    {
        _logger = new Mock<ILogger<CreateBookingCommandHandler>>();
        _backgroundTaskQueue = new Mock<IBackgroundTaskQueue>();
        _bookingRepository = new Mock<IBookingRepository>();
        _eventRepository = new Mock<IEventsRepository>();

        _commandHandler = new CreateBookingCommandHandler(_bookingRepository.Object, _eventRepository.Object, _backgroundTaskQueue.Object, _logger.Object);
    }


    [Fact]
    public async Task Handle_WhenValid_ReturnsCreated()
    {
        var seatId = 1;
        var eventId = 2;
        var @event = new Event
        {
            Id = eventId,
            EventDate = DateTime.UtcNow.AddDays(10)
        };

        var userId = Guid.NewGuid().ToString();
        var command = new CreateBookingCommand(eventId, seatId, userId);

        var seat = new Seat { Id = seatId, Event = @event };
        Booking? createdBooking = null;

        _eventRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _bookingRepository
            .Setup(x => x.SaveChangesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _bookingRepository
            .Setup(x => x.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Callback<Booking, CancellationToken>((booking, _) => createdBooking = booking)
            .Returns(Task.CompletedTask);

        _bookingRepository
            .Setup(x => x.GetSeatForBookingAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        _backgroundTaskQueue
            .Setup(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var result = await _commandHandler.Handle(command, CancellationToken.None);

        _eventRepository.Verify(x => x.GetEventAsync(eventId, CancellationToken.None), Times.Once);
        _bookingRepository.Verify(x => x.GetSeatForBookingAsync(eventId, seatId, CancellationToken.None), Times.Once);
        _bookingRepository.Verify(x => x.AddAsync(It.Is<Booking>(b => b.UserId == userId && b.SeatId == seatId), It.IsAny<CancellationToken>()), Times.Once);
        _bookingRepository.Verify(x => x.SaveChangesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
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
        var eventId = 2;
        var userId = Guid.NewGuid().ToString();
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var @event = new Event { Id = eventId, EventDate = DateTime.UtcNow.AddDays(10) };

        var seat = new Seat
        {
            Id = seatId,
            Event = @event
        };

        var command = new CreateBookingCommand(eventId, seatId, userId);

        _eventRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _bookingRepository
            .Setup(x => x.GetSeatForBookingAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        _bookingRepository
            .Setup(x => x.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _bookingRepository
            .Setup(x => x.SaveChangesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _backgroundTaskQueue
            .Setup(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        await _commandHandler.Handle(command, cancellationToken);

        _eventRepository.Verify(x => x.GetEventAsync(eventId, cancellationToken), Times.Once);

        _bookingRepository.Verify(x => x.GetSeatForBookingAsync(eventId, seatId, cancellationToken), Times.Once);

        _bookingRepository.Verify(x => x.AddAsync(It.IsAny<Booking>(), cancellationToken), Times.Once);

        _bookingRepository.Verify(x => x.SaveChangesAsync(It.IsAny<int>(), cancellationToken), Times.Once);

        _backgroundTaskQueue.Verify(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_PropagatesException()
    {
        var seatId = 1;
        var eventId = 2;
        var userId = Guid.NewGuid().ToString();
        var @event = new Event { Id = eventId, EventDate = DateTime.UtcNow.AddDays(10) };
        var bookingCommand = new CreateBookingCommand(eventId, seatId, userId);
        var seat = new Seat { Id = seatId, Event = @event };

        _bookingRepository
            .Setup(x => x.GetSeatForBookingAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        _eventRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        var exception = new InvalidOperationException("Database unavailable");

        _bookingRepository
            .Setup(x => x.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _commandHandler.Handle(bookingCommand, CancellationToken.None));

        _backgroundTaskQueue.Verify(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()), Times.Never);
        _bookingRepository.Verify(x => x.SaveChangesAsync(seatId, CancellationToken.None), Times.Never);
        _bookingRepository.Verify(x => x.GetSeatForBookingAsync(eventId, seatId, CancellationToken.None), Times.Once);
        _eventRepository.Verify(x => x.GetEventAsync(eventId, CancellationToken.None), Times.Once);

        Assert.Equal("Database unavailable", result.Message);
    }

    [Fact]
    public async Task Handle_WhenSeatNotFound_ReturnsSeatNotFound()
    {
        var seatId = 5;
        var eventId = 1;
        var userId = Guid.NewGuid().ToString();
        var @event = new Event { Id = eventId, EventDate = DateTime.UtcNow.AddDays(10) };

        _eventRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _bookingRepository
            .Setup(x => x.GetSeatForBookingAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Seat?)null);

        var result = await _commandHandler.Handle(new CreateBookingCommand(eventId, seatId, userId), CancellationToken.None);
        _eventRepository.Verify(x => x.GetEventAsync(eventId, CancellationToken.None), Times.Once);
        _bookingRepository.Verify(x => x.AddAsync(It.Is<Booking>(b => b.UserId == userId && b.SeatId == seatId), It.IsAny<CancellationToken>()), Times.Never);
        _bookingRepository.Verify(x => x.SaveChangesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _backgroundTaskQueue.Verify(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.IsType<BookingSeatNotFound>(result);
    }

    [Fact]
    public async Task Handle_WhenSeatAlreadyBooked_ReturnsAlreadyBooked()
    {
        var eventId = 1;
        var seat = new Seat { Id = 1, Booking = Booking.Create("user-id", 1, DateTime.UtcNow), Event = new Event { EventDate = DateTime.UtcNow.AddDays(10) } };
        var userId = Guid.NewGuid().ToString();

        var @event = new Event { Id = eventId, EventDate = DateTime.UtcNow.AddDays(10) };

        _eventRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _bookingRepository
            .Setup(x => x.GetSeatForBookingAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        var result = await _commandHandler.Handle(new CreateBookingCommand(1, seat.Id, userId), CancellationToken.None);

        _eventRepository.Verify(x => x.GetEventAsync(eventId, CancellationToken.None), Times.Once);
        _bookingRepository.Verify(x => x.AddAsync(It.Is<Booking>(b => b.UserId == userId && b.SeatId == seat.Id), It.IsAny<CancellationToken>()), Times.Never);
        _bookingRepository.Verify(x => x.SaveChangesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _backgroundTaskQueue.Verify(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.IsType<BookingSeatAlreadyBooked>(result);
    }

    [Fact]
    public async Task Handle_WhenEventUnavailable_ReturnsEventUnavailable()
    {
        var eventId = 1;
        var @event = new Event { Id = eventId, EventDate = DateTime.UtcNow.AddDays(-10) };

        _eventRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        var seat = new Seat { Id = 1, Event = @event };
        var userId = Guid.NewGuid().ToString();

        _bookingRepository
            .Setup(x => x.GetSeatForBookingAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        var result = await _commandHandler.Handle(new CreateBookingCommand(1, seat.Id, userId), CancellationToken.None);

        _bookingRepository.Verify(x => x.AddAsync(It.Is<Booking>(b => b.UserId == userId && b.SeatId == seat.Id), It.IsAny<CancellationToken>()), Times.Never);
        _eventRepository.Verify(x => x.GetEventAsync(eventId, CancellationToken.None), Times.Once);
        _bookingRepository.Verify(x => x.SaveChangesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _backgroundTaskQueue.Verify(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.IsType<BookingEventUnavailable>(result);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesThrowsError_BackgroundTaskNotQueued()
    {
        var seatId = 1;
        var eventId = 1;
        Booking? createdBooking = null;
        var message = "something went wrong while persisting the booking";
        var exception = new InvalidOperationException(message);
        var userId = Guid.NewGuid().ToString();
        var @event = new Event { Id = eventId, EventDate = DateTime.UtcNow.AddDays(10) };
        var seat = new Seat { Id = seatId, Event = @event };

        _eventRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _bookingRepository
            .Setup(x => x.SaveChangesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        _bookingRepository
            .Setup(x => x.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Callback<Booking, CancellationToken>((booking, _) => createdBooking = booking)
            .Returns(Task.CompletedTask);

        _bookingRepository
            .Setup(x => x.GetSeatForBookingAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        _backgroundTaskQueue
            .Setup(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() => _commandHandler.Handle(new CreateBookingCommand(1, seatId, userId), CancellationToken.None));
        Assert.Equal(message, result.Message);

        _eventRepository.Verify(x => x.GetEventAsync(eventId, CancellationToken.None), Times.Once);
        _bookingRepository.Verify(x => x.AddAsync(It.Is<Booking>(b => b.UserId == userId && b.SeatId == seatId), It.IsAny<CancellationToken>()), Times.Once);
        _bookingRepository.Verify(x => x.SaveChangesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        _backgroundTaskQueue.Verify(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), CancellationToken.None), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEventDoesNotExist_ReturnsBookingEventNotFound()
    {
        var seatId = 1;
        var eventId = 1;
        var userId = Guid.NewGuid().ToString();

        _eventRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var result = await _commandHandler.Handle(new CreateBookingCommand(eventId, seatId, userId), CancellationToken.None);

        Assert.IsType<BookingEventNotFound>(result);
        _eventRepository.Verify(x => x.GetEventAsync(eventId, CancellationToken.None), Times.Once);
        _bookingRepository.Verify(x => x.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
        _bookingRepository.Verify(x => x.SaveChangesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _backgroundTaskQueue.Verify(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), CancellationToken.None), Times.Never);

    }

    [Fact]
    public async Task Handle_WhenSeatNotFoundForEvent_ReturnsSeatNotFound()
    {
        var eventId = 1;
        var seatId = 10;
        var userId = Guid.NewGuid().ToString();

        _eventRepository
            .Setup(x => x.GetEventAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Event
            {
                Id = eventId,
                EventDate = DateTime.UtcNow.AddDays(10)
            });

        _bookingRepository
            .Setup(x => x.GetSeatForBookingAsync(eventId, seatId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Seat?)null);

        var result = await _commandHandler.Handle(new CreateBookingCommand(eventId, seatId, userId), CancellationToken.None);

        Assert.IsType<BookingSeatNotFound>(result);

        _eventRepository.Verify(x => x.GetEventAsync(eventId, CancellationToken.None), Times.Once);
        _bookingRepository.Verify(x => x.AddAsync(It.Is<Booking>(b => b.UserId == userId), It.IsAny<CancellationToken>()), Times.Never);
        _bookingRepository.Verify(x => x.SaveChangesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _backgroundTaskQueue.Verify(x => x.QueueAsync(It.IsAny<BackgroundWorkItem>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.IsType<BookingSeatNotFound>(result);
    }
}
