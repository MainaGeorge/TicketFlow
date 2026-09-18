using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Events.Interfaces;
using TicketFlow.Application.Events.Models;
using TicketFlow.Application.Events.Queries.GetEvent;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Events.Queries.GetEvent;

public class GetEventQueryHandlerTests
{
    private readonly Mock<ILogger<GetEventQueryHandler>> _logger;
    private readonly Mock<IEventsRepository> _eventsRepository;
    private readonly GetEventQueryHandler _handler;

    public GetEventQueryHandlerTests()
    {
        _logger = new Mock<ILogger<GetEventQueryHandler>>();
        _eventsRepository = new Mock<IEventsRepository>();
        _handler = new GetEventQueryHandler(_eventsRepository.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_WhenEventExists_ReturnsEvent()
    {
        var eventId = 1;
        var @event = new Event {  Id = eventId, Venue = "Emirates Stadium", Name = "FA Cup Final" };

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        var result = await _handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        var retrievedEvent = Assert.IsType<EventResult>(result);

        Assert.Equal(@event.Name, retrievedEvent.Event!.Name);
        Assert.Equal(@event.Id, retrievedEvent.Event.Id);
        Assert.Equal(@event.Venue, retrievedEvent.Event!.Venue);

        _eventsRepository.Verify(x => x.GetEventAsync(eventId, CancellationToken.None), Times.Once());
    }

    [Fact]
    public async Task Handle_WhenEventDoesNotExist_ReturnsEventNotFound()
    {
        var eventId = 1;

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?) null);

        var result = await _handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        var retrievedEvent = Assert.IsType<EventNotFoundResult>(result);
        _eventsRepository.Verify(x => x.GetEventAsync(eventId, CancellationToken.None), Times.Once());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepository()
    {
        var eventId = 1;
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var result = await _handler.Handle(new GetEventQuery(eventId), token);

        _eventsRepository.Verify(x => x.GetEventAsync(eventId, token), Times.Once());
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_PropagatesException()
    {
        var eventId = 1;
        var message = "something went wrong while querying the db";
        var exception = new Exception(message);

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var error = await Assert.ThrowsAsync<Exception>(() => _handler.Handle(new GetEventQuery(eventId), CancellationToken.None));

        Assert.Equal(message, error.Message);
        Assert.Same(exception, error);
        _eventsRepository.Verify(x => x.GetEventAsync(eventId, CancellationToken.None), Times.Once());
    }
}
