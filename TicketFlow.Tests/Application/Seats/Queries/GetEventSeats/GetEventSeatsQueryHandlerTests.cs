using Moq;
using TicketFlow.Application.Abstractions.Repositories;
using TicketFlow.Application.Seats.Queries.GetEventSeats;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Seats.Queries.GetEventSeats;

public class GetEventSeatsQueryHandlerTests
{
    private readonly Mock<ISeatsRepository> _repository;
    private readonly GetEventSeatsQueryHandler _handler;

    public GetEventSeatsQueryHandlerTests()
    {
        _repository = new Mock<ISeatsRepository>();
        _handler = new GetEventSeatsQueryHandler(_repository.Object);
    }

    [Fact]
    public async Task Handle_WhenEventSeatsExist_ReturnsACollectionOfSeatResults()
    {
        var seats = new List<Seat>() { new() { Id = 1, EventId = 1 , Number = 2, Row = "A"}, new() { Id = 2, EventId = 1, Number = 1, Row = "A" } };

        _repository
            .Setup(x => x.GetSeatsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seats);

        var query = new GetEventSeatsQuery(1);

        var results = await _handler.Handle(query, CancellationToken.None);

        Assert.Collection(
            results,
            first =>
            {
                Assert.Equal(1, first.Seat.Id);
                Assert.Equal(1, first.Seat.EventId);
                Assert.Equal(2, first.Seat.Number);
                Assert.Equal("A", first.Seat.Row);
            },
            second =>
            {
                Assert.Equal(2, second.Seat.Id);
                Assert.Equal(1, second.Seat.EventId);
                Assert.Equal(1, second.Seat.Number);
                Assert.Equal("A", second.Seat.Row);
            });

        _repository.Verify(x => x.GetSeatsAsync(1, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEventSeatsDontExist_ReturnsEmptyCollection()
    { 
        _repository
            .Setup(x => x.GetSeatsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var query = new GetEventSeatsQuery(1);

        var results = await _handler.Handle(query, CancellationToken.None);

        Assert.Empty(results);

        _repository.Verify(x => x.GetSeatsAsync(1, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        _repository
            .Setup(x => x.GetSeatsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        var query = new GetEventSeatsQuery(1);
        await _handler.Handle(query, token);

        _repository.Verify(x => x.GetSeatsAsync(1, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_PropagatesException()
    {
        var message = "not again db!!";
        var exception = new InvalidOperationException(message);

        _repository
            .Setup(x => x.GetSeatsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var query = new GetEventSeatsQuery(1);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(query, CancellationToken.None));

        Assert.Same(exception, error);
        _repository.Verify(x => x.GetSeatsAsync(1, CancellationToken.None), Times.Once);
    }
}
