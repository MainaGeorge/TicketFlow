using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Abstractions.Repositories;
using TicketFlow.Application.Seats.Commands;
using TicketFlow.Application.Seats.Exceptions;
using TicketFlow.Application.Seats.Models;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Seats.Commands;

public class CreateSeatCommandHandlerTests
{
    private readonly Mock<IEventsRepository> _eventsRepository;
    private readonly Mock<ISeatsRepository> _seatsRepository;
    private readonly Mock<ILogger<CreateSeatsCommandHandler>> _logger;
    private readonly CreateSeatsCommandHandler _handler;

    public CreateSeatCommandHandlerTests()
    {
        _eventsRepository = new Mock<IEventsRepository>();
        _seatsRepository = new Mock<ISeatsRepository>();
        _logger = new Mock<ILogger<CreateSeatsCommandHandler>>();
        _handler = new CreateSeatsCommandHandler(_seatsRepository.Object, _eventsRepository.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_WhenMultipleSeatsAreValid_CreatesAllSeats()
    {
        var eventId = 1;

        var @event = new Event
        {
            Id = eventId,
            Name = "Shakira Concert",
            Venue = "Emirates Stadium"
        };

        var seatItems = new List<CreateSeatItem>
        {
            new(" a ", 1, 100m),
            new("B", 2, 150m),
            new(" c ", 3, 200m)
        };

        List<Seat>? capturedSeats = null;

        _eventsRepository
            .Setup(x => x.GetEventAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _seatsRepository
            .Setup(x => x.CreateSeatsAsync(
                It.IsAny<IEnumerable<Seat>>(),
                It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<Seat> seats, CancellationToken _) => capturedSeats = [.. seats])
            .ReturnsAsync((IEnumerable<Seat> seats, CancellationToken _) => seats);

        var result = await _handler.Handle(
            new CreateSeatsCommand(eventId, seatItems),
            CancellationToken.None);

        Assert.IsType<SeatsCreatedResult>(result);

        Assert.NotNull(capturedSeats);
        Assert.Equal(3, capturedSeats.Count);

        Assert.Collection(
            capturedSeats,
            seat =>
            {
                Assert.Equal(eventId, seat.EventId);
                Assert.Equal("A", seat.Row);
                Assert.Equal(1, seat.Number);
                Assert.Equal(100m, seat.Price);
            },
            seat =>
            {
                Assert.Equal(eventId, seat.EventId);
                Assert.Equal("B", seat.Row);
                Assert.Equal(2, seat.Number);
                Assert.Equal(150m, seat.Price);
            },
            seat =>
            {
                Assert.Equal(eventId, seat.EventId);
                Assert.Equal("C", seat.Row);
                Assert.Equal(3, seat.Number);
                Assert.Equal(200m, seat.Price);
            });
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

        var seats = new List<CreateSeatItem> { new(seatRow, seatNumber, seatPrice) };

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _seatsRepository
            .Setup(x => x.CreateSeatsAsync(It.IsAny<IEnumerable<Seat>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Seat> s, CancellationToken _) => s);

        var result = await _handler.Handle(new CreateSeatsCommand(eventId, seats), CancellationToken.None);

        Assert.IsType<SeatsCreatedResult>(result);

        _eventsRepository.Verify(x => x.GetEventAsync(1, CancellationToken.None), Times.Once);
        _seatsRepository.Verify(x => x.CreateSeatsAsync(
            It.Is<List<Seat>>(s => s[0].EventId == @event.Id && s[0].Row == seatRow && s[0].Number == seatNumber && s[0].Price == seatPrice),
            CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData("    A    ")]
    [InlineData("    a    ")]
    [InlineData("a    ")]
    [InlineData("    a")]
    [InlineData("a")]
    public async Task Handle_WhenSeatsAreCreated_NormalizesRows(string? row)
    {
        var eventId = 1;
        var seatRow = row;
        var seatNumber = 12;
        var seatPrice = 100m;

        List<Seat>? seats = null;

        var @event = new Event
        {
            Id = eventId,
            Name = "Shakira Concert",
            Venue = "Emirates Stadium"
        };

        var seatItems = new List<CreateSeatItem> { new(seatRow!, seatNumber, seatPrice) };

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _seatsRepository
            .Setup(x => x.CreateSeatsAsync(It.IsAny<IEnumerable<Seat>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<Seat> s, CancellationToken cancellationToken) => seats = [.. s])
            .ReturnsAsync([]);

        var result = await _handler.Handle(new CreateSeatsCommand(eventId, seatItems), CancellationToken.None);

        Assert.NotNull(seats);
        Assert.Contains(seats, s => s.Row.Equals(row!.Trim(), StringComparison.InvariantCultureIgnoreCase));
    }

    [Fact]
    public async Task Handle_WhenRepositoryDetectsDuplicate_ReturnsDuplicateSeatResult()
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

        var exception = new DuplicateSeatException();

        var seats = new List<CreateSeatItem> { new(seatRow, seatNumber, seatPrice) };

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _seatsRepository
            .Setup(x => x.CreateSeatsAsync(It.IsAny<IEnumerable<Seat>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await _handler.Handle(new CreateSeatsCommand(eventId, seats), CancellationToken.None);

        Assert.IsType<DuplicateSeatResult>(result);

        _eventsRepository.Verify(x => x.GetEventAsync(1, CancellationToken.None), Times.Once);
        _seatsRepository.Verify(x => x.CreateSeatsAsync(
            It.Is<List<Seat>>(s => s[0].EventId == @event.Id && s[0].Row == seatRow && s[0].Number == seatNumber && s[0].Price == seatPrice),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEventNotFound_ReturnsEventNotFoundResult()
    {
        var (eventId, seatRow, seatNumber, seatPrice) = (1, "A", 12, 200);
        var seats = new List<CreateSeatItem> { new(seatRow, seatNumber, seatPrice) };

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var result = await _handler.Handle(new CreateSeatsCommand(eventId, seats), CancellationToken.None);

        var created = Assert.IsType<EventNotFoundForSeatResult>(result);

        _eventsRepository.Verify(x => x.GetEventAsync(1, CancellationToken.None), Times.Once);
        _seatsRepository.Verify(x => x.CreateSeatsAsync(It.IsAny<IEnumerable<Seat>>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Handle_WhenEventRepositoryThrows_PropagatesError()
    {
        var (eventId, seatRow, seatNumber, seatPrice) = (1, "A", 12, 200);
        var seats = new List<CreateSeatItem> { new(seatRow, seatNumber, seatPrice) };
        var message = "db is acting out again!";
        var exception = new InvalidOperationException(message);

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(new CreateSeatsCommand(eventId, seats), CancellationToken.None));

        _eventsRepository.Verify(x => x.GetEventAsync(1, CancellationToken.None), Times.Once);
        _seatsRepository.Verify(x => x.CreateSeatsAsync(It.IsAny<IEnumerable<Seat>>(), It.IsAny<CancellationToken>()), Times.Never());
        Assert.Equal(message, error.Message);
        Assert.Same(exception, error);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepository()
    {
        var (eventId, seatRow, seatNumber, seatPrice) = (1, "A", 12, 200);
        var seats = new List<CreateSeatItem> { new(seatRow, seatNumber, seatPrice) };
        var @event = new Event { Id = 1, Name = "Shakira Concert", Venue = "Emirates Stadium" };
        var seat = new Seat { EventId = eventId, Id = 2, Row = seatRow, Number = seatNumber, Price = seatPrice };
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _seatsRepository
            .Setup(x => x.CreateSeatsAsync(It.IsAny<IEnumerable<Seat>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([seat]);

        var result = await _handler.Handle(new CreateSeatsCommand(eventId, seats), token);


        _seatsRepository.Verify(x => x.CreateSeatsAsync(It.Is<List<Seat>>(s => s[0].Row == seatRow && s[0].Number == seatNumber && s[0].Price == seatPrice), token), Times.Once());
        _eventsRepository.Verify(x => x.GetEventAsync(1, token), Times.Once);
    }
}
