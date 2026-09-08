using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Events;
using TicketFlow.Application.Seats;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application;

public class SeatsServiceTests
{
    private readonly Mock<ILogger<SeatsService>> _logger;
    private readonly Mock<ISeatsRepository> _seatsRepository;
    private readonly Mock<IEventsRepository> _eventsRepository;
    private readonly SeatsService _seatsService;

    public SeatsServiceTests()
    {
        _logger = new Mock<ILogger<SeatsService>>();
        _seatsRepository = new Mock<ISeatsRepository>();
        _eventsRepository = new Mock<IEventsRepository>();

        _seatsService = new SeatsService(_logger.Object, _seatsRepository.Object, _eventsRepository.Object);
    }

    [Fact]
    public async Task GetSeats_WhenSeatsExist_ReturnsSeats()
    {
        var seat = new Seat { Id = 1 };

        _seatsRepository
            .Setup(x => x.GetSeatsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([seat]);

        var seats = await _seatsService.GetSeatsAsync(1, CancellationToken.None);

        var result = Assert.IsType<IEnumerable<SeatResult>>(seats, exactMatch: false);
        Assert.Contains(result, r => r.Seat!.Id == seat.Id);
    }

    [Fact]
    public async Task GetSeats_WhenSeatsDontExist_ReturnsEmptyCollectionOfSeatResult()
    {
        _seatsRepository
            .Setup(x => x.GetSeatsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var seats = await _seatsService.GetSeatsAsync(1, CancellationToken.None);

        var result = Assert.IsType<IEnumerable<SeatResult>>(seats, exactMatch: false);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSeat_WhenSeatExists_ReturnsSeatResult()
    {
        var seat = new Seat { Id = 1, EventId = 2 };

        _seatsRepository
            .Setup(x => x.GetSeatAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        var seats = await _seatsService.GetSeatAsync(seat.Id, seat.EventId, CancellationToken.None);

        var result = Assert.IsType<SeatResult>(seats);
        Assert.Equal(seat.Id, result.Seat!.Id);
    }

    [Fact]
    public async Task GetSeat_WhenSeatDoesNotExist_ReturnsSeatNotFound()
    {
        _seatsRepository
            .Setup(x => x.GetSeatAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Seat?)null);

        var seats = await _seatsService.GetSeatAsync(1, 1, CancellationToken.None);

        var result = Assert.IsType<SeatNotFound>(seats);
    }

    [Fact]
    public async Task CreateSeat_WhenValid_ReturnsCreatedResult()
    {
        var @event = new Event { Id = 1, Name = "Shakira Concert" };
        var seat = new Seat { EventId = @event.Id, Id = 2 };

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>()))
            .ReturnsAsync(@event);

        _seatsRepository
            .Setup(x => x.CreateSeatAsync(It.IsAny<Seat>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        var result = await _seatsService.CreateSeatAsync(@event.Id, seat, CancellationToken.None);

        var created = Assert.IsType<SeatCreatedResult>(result);

        _seatsRepository.Verify(x => x.CreateSeatAsync(It.Is<Seat>(s => s.Id == seat.Id), It.IsAny<CancellationToken>()), Times.Once());
        Assert.Equal(@event.Id, created.Seat!.EventId);
        Assert.Equal(seat.Id, created.Seat!.Id);
    }

    [Fact]
    public async Task CreateSeat_WhenEventNotFound_ReturnsEventNotFoundResult()
    {

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>()))
            .ReturnsAsync((Event?) null);

        var result = await _seatsService.CreateSeatAsync(1, new Seat(), CancellationToken.None);

        var created = Assert.IsType<EventNotFoundForSeatResult>(result);

        _seatsRepository.Verify(x => x.CreateSeatAsync(It.IsAny<Seat>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
