using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Seats;
using TicketFlow.Application.Seats.Interfaces;
using TicketFlow.Application.Seats.Queries.GetSeat;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Seats.Queries.GetSeat;

public class GetSeatQueryHandlerTests
{
    private readonly Mock<ILogger<GetSeatQueryHandler>> _logger;
    private readonly Mock<ISeatsRepository> _seatsRepository;
    private readonly GetSeatQueryHandler _handler;

    public GetSeatQueryHandlerTests()
    {
        _logger = new Mock<ILogger<GetSeatQueryHandler>>();
        _seatsRepository = new Mock<ISeatsRepository>();
        _handler = new GetSeatQueryHandler(_seatsRepository.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_WhenSeatExists_ReturnsSeatResult()
    {
        var seatId = 1;
        var eventId = 1;

        var seat = new Seat { Id = seatId, EventId = eventId, Row = "A", Number = 12, Price = 20m, Booking = Booking.Create("", 1, DateTime.UtcNow) };

        _seatsRepository
            .Setup(x => x.GetSeatAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        var query = new GetSeatQuery(eventId, seatId);

        var result = await _handler.Handle(query, CancellationToken.None);

        var retrievedSeat = Assert.IsType<SeatResult>(result);

        Assert.Equal(seat.Id, retrievedSeat.Seat!.Id);
        Assert.Equal(seat.EventId, retrievedSeat.Seat!.EventId);
        Assert.Equal(seat.Number, retrievedSeat.Seat!.Number);
        Assert.Equal(seat.Price, retrievedSeat.Seat!.Price);

        _seatsRepository.Verify(x => x.GetSeatAsync(eventId, seatId, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSeatDoesNotExist_ReturnsSeatNotFound()
    {
        var seatId = 1;
        var eventId = 1;

        _seatsRepository
            .Setup(x => x.GetSeatAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Seat?)null);

        var query = new GetSeatQuery(eventId, seatId);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.IsType<SeatNotFound>(result);

        _seatsRepository.Verify(x => x.GetSeatAsync(eventId, seatId, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_PropagatesException()
    {
        var seatId = 1;
        var eventId = 1;
        var message = "db acting up again!";
        var exception = new InvalidDataException(message);

        _seatsRepository
            .Setup(x => x.GetSeatAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var query = new GetSeatQuery(eventId, seatId);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => _handler.Handle(query, CancellationToken.None));

        Assert.Same(exception, error);

        _seatsRepository.Verify(x => x.GetSeatAsync(eventId, seatId, CancellationToken.None), Times.Once);
    }
    [Fact]
    public async Task Handle_PassesCancellationTokenToRepository()
    {
        var seatId = 1;
        var eventId = 1;

        var seat = new Seat
        {
            Id = seatId,
            EventId = eventId,
            Row = "A",
            Number = 12,
            Price = 20m
        };

        using var source = new CancellationTokenSource();
        var token = source.Token;

        _seatsRepository.Setup(x => x.GetSeatAsync(eventId, seatId, token))
            .ReturnsAsync(seat);

        var query = new GetSeatQuery(eventId, seatId);

        await _handler.Handle(query, token);

        _seatsRepository.Verify(x => x.GetSeatAsync(eventId, seatId, token), Times.Once);
    }
}
