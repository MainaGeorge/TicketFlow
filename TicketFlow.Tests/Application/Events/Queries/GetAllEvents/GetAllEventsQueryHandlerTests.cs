using Moq;
using TicketFlow.Application.Events.Interfaces;
using TicketFlow.Application.Events.Models;
using TicketFlow.Application.Events.Queries.GetAllEvents;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Events.Queries.GetEvents;

public class GetAllEventsQueryHandlerTests
{
    private readonly Mock<IEventsRepository> _repository;
    private readonly GetAllEventsQueryHandler _handler;

    public GetAllEventsQueryHandlerTests()
    {
        _repository = new Mock<IEventsRepository>();
        _handler = new GetAllEventsQueryHandler(_repository.Object);
    }

    [Fact]
    public async Task Handle_WhenEventsDontExist_ReturnsEmptyCollection()
    {
        _repository
            .Setup(x => x.GetAllEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var query = new GetAllEventsQuery();

        var result = await _handler.Handle(query, CancellationToken.None);

        var retrievedEvents = Assert.IsType<IEnumerable<EventResult>>(result, exactMatch: false);
        Assert.Empty(retrievedEvents);
        _repository.Verify(x => x.GetAllEventsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEventsExist_ReturnsEvents()
    {
        var events = new List<Event> { new() { Id = 1 }, new() { Id = 2 } };

        _repository
            .Setup(x => x.GetAllEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        var query = new GetAllEventsQuery();

        var result = await _handler.Handle(query, CancellationToken.None);

        var retrievedEvents = Assert.IsType<IEnumerable<EventResult>>(result, exactMatch: false);
        Assert.Equal(2, retrievedEvents.Count());
        Assert.Contains(retrievedEvents, e => e.Event!.Id == 1);
        Assert.Collection(result, first => Assert.Equal(1, first.Event!.Id), second => Assert.Equal(2, second.Event!.Id));

        _repository.Verify(x => x.GetAllEventsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepository()
    {
        var events = new List<Event> { new() { Id = 1 }, new() { Id = 2 } };
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        _repository
            .Setup(x => x.GetAllEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        var query = new GetAllEventsQuery();

        var result = await _handler.Handle(query, token);

        _repository.Verify(x => x.GetAllEventsAsync(token), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_PropagatesException()
    {
        var message = "something went wrong while querying the db";
        var exception = new Exception(message);

        _repository
            .Setup(x => x.GetAllEventsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var error = await Assert.ThrowsAsync<Exception>(() => _handler.Handle(new GetAllEventsQuery(), CancellationToken.None));

        Assert.Equal(message, error.Message);
        Assert.Same(exception, error);
    }
}
