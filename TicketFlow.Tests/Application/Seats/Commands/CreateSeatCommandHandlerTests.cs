using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Events.Interfaces;
using TicketFlow.Application.Seats;
using TicketFlow.Application.Seats.Commands;
using TicketFlow.Application.Seats.Interfaces;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Seats.Commands;

public class CreateSeatCommandHandlerTests
{
    private readonly Mock<IEventsRepository> _eventsRepository;
    private readonly Mock<ISeatsRepository> _seatsRepository;
    private readonly Mock<ILogger<CreateSeatCommandHandler>> _logger;
    private readonly CreateSeatCommandHandler _handler;

    public CreateSeatCommandHandlerTests()
    {
        _eventsRepository = new Mock<IEventsRepository>();
        _seatsRepository = new Mock<ISeatsRepository>();
        _logger = new Mock<ILogger<CreateSeatCommandHandler>>();
        _handler = new CreateSeatCommandHandler(_seatsRepository.Object, _eventsRepository.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_WhenValid_ReturnsCreatedResult()
    {
        var eventId = 1;
        var seatRow = "A";
        var seatNumber = 12;
        var seatPrice = 100m;

        var @event = new Event
        {
            Id = eventId,
            Name = "Shakira Concert",
            Venue = "Emirates Stadium"
        };

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _seatsRepository
            .Setup(x => x.CreateSeatAsync(It.IsAny<Seat>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Seat s, CancellationToken _) => s);

        var result = await _handler.Handle(new CreateSeatCommand(eventId, seatRow, seatNumber, seatPrice), CancellationToken.None);

        Assert.IsType<SeatCreatedResult>(result);

        _eventsRepository.Verify(x => x.GetEventAsync(1, CancellationToken.None), Times.Once);
        _seatsRepository.Verify( x => x.CreateSeatAsync(
            It.Is<Seat>(s =>
                s.EventId == @event.Id &&
                s.Row == seatRow &&
                s.Number == seatNumber &&
                s.Price == seatPrice),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CreateSeat_WhenEventNotFound_ReturnsEventNotFoundResult()
    {
        var (eventId, seatRow, seatNumber, seatPrice) = (1, "A", 12, 200);

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var result = await _handler.Handle(new CreateSeatCommand(eventId, seatRow, seatNumber, seatPrice), CancellationToken.None);

        var created = Assert.IsType<EventNotFoundForSeatResult>(result);

        _eventsRepository.Verify(x => x.GetEventAsync(1, CancellationToken.None), Times.Once);
        _seatsRepository.Verify(x => x.CreateSeatAsync(It.IsAny<Seat>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_PropagatesError()
    {
        var (eventId, seatRow, seatNumber, seatPrice) = (1, "A", 12, 200);
        var message = "db is acting out again!";
        var exception = new InvalidOperationException(message);

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(new CreateSeatCommand(eventId, seatRow, seatNumber, seatPrice), CancellationToken.None));

        _eventsRepository.Verify(x => x.GetEventAsync(1, CancellationToken.None), Times.Once);
        _seatsRepository.Verify(x => x.CreateSeatAsync(It.IsAny<Seat>(), It.IsAny<CancellationToken>()), Times.Never());
        Assert.Equal(message, error.Message);
        Assert.Same(exception, error);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepository()
    {
        var (eventId, seatRow, seatNumber, seatPrice) = (1, "A", 12, 200);
        var @event = new Event { Id = 1, Name = "Shakira Concert", Venue = "Emirates Stadium" };
        var seat = new Seat { EventId = eventId, Id = 2, Row = seatRow, Number = seatNumber, Price = seatPrice };
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _seatsRepository
            .Setup(x => x.CreateSeatAsync(It.IsAny<Seat>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        var result = await _handler.Handle(new CreateSeatCommand(eventId, seatRow, seatNumber, seatPrice), token);


        _seatsRepository.Verify(x => x.CreateSeatAsync(It.Is<Seat>(s => s.Row == seatRow && s.Price == seatPrice && s.Number == seat.Number), token), Times.Once());
        _eventsRepository.Verify(x => x.GetEventAsync(1, token), Times.Once);
    }
}
