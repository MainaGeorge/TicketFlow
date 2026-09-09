using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Events.Interfaces;
using TicketFlow.Application.Events.Models;
using TicketFlow.Application.Events.Services;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application;

public class EventsServiceTests
{
    private readonly Mock<IEventsRepository> _eventRepository;
    private readonly Mock<ILogger<EventsService>> _logger;
    private readonly EventsService _eventsService;
    private const string UserId = "userId";

    public EventsServiceTests()
    {
        _eventRepository = new Mock<IEventsRepository>();
        _logger = new Mock<ILogger<EventsService>>();

        _eventsService = new EventsService(_eventRepository.Object, _logger.Object);
    }

    [Fact]
    public async Task GetEvents_WhenCalled_PassesCancellationToken()
    {
        var tokenSource = new CancellationTokenSource();
        var token = tokenSource.Token;

        _eventRepository
            .Setup(x => x.GetAllEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _eventsService.GetAllEventsAsync(token);

        var retrievedEvents = Assert.IsType<IEnumerable<EventResult>>(result, exactMatch: false);
        _eventRepository.Verify(x => x.GetAllEventsAsync(token), Times.Once);
        Assert.Empty(retrievedEvents);
    }

    [Fact]
    public async Task GetEvents_WhenTokenCancelled_PassesCancelledToken()
    {
        var tokenSource = new CancellationTokenSource();
        tokenSource.Cancel();

        _eventRepository
            .Setup(x => x.GetAllEventsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(tokenSource.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() => _eventsService.GetAllEventsAsync(tokenSource.Token));
    }

    [Fact]
    public async Task GetEvents_WhenRepositoryThrows_PropagatesException()
    {
        var message = "Unexpected error has happened.";
        var exception = new InvalidOperationException(message);

        _eventRepository
            .Setup(x => x.GetAllEventsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() => _eventsService.GetAllEventsAsync(CancellationToken.None));
        Assert.Equal(message, result.Message);
    }

    [Fact]
    public async Task GetEvents_WhenEventsExists_ReturnsEvents()
    {
        var events = new List<Event> { new() { Id = 1 }, new() { Id = 2 } };

        _eventRepository
            .Setup(x => x.GetAllEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        var result = await _eventsService.GetAllEventsAsync(CancellationToken.None);

        var retrievedEvents = Assert.IsType<IEnumerable<EventResult>>(result, exactMatch: false);
        Assert.Equal(2, retrievedEvents.Count());
        Assert.Contains(retrievedEvents, e => e.Event!.Id == 1);
    }

    [Fact]
    public async Task GetEvents_WhenEventsDontExist_ReturnsNoEvents()
    {
        _eventRepository
            .Setup(x => x.GetAllEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _eventsService.GetAllEventsAsync(CancellationToken.None);

        var retrievedEvents = Assert.IsType<IEnumerable<EventResult>>(result, exactMatch: false);
        Assert.Empty(retrievedEvents);
    }

    [Fact]
    public async Task GetEvent_WhenEventExists_ReturnsEvent()
    {
        var @event = new Event { Id = 1, Name = "Busta Rhymes Concert" };

        _eventRepository
            .Setup(x => x.GetEventAsync(It.Is<int>(d => d == @event.Id)))
            .ReturnsAsync(@event);

        var result = await _eventsService.GetEventAsync(@event.Id, CancellationToken.None);

        var retrievedEvent = Assert.IsType<EventResult>(result);
        Assert.Equal(retrievedEvent.Event!.Id, @event.Id);
    }

    [Fact]
    public async Task GetEvent_WhenEventDontExist_ReturnsEventNotFound()
    {

        _eventRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>()))
            .ReturnsAsync((Event?)null);

        var result = await _eventsService.GetEventAsync(1, CancellationToken.None);

        var retrievedEvent = Assert.IsType<EventNotFoundResult>(result);
    }

    [Fact]
    public async Task CreateEvent_WhenValid_ReturnsEventCreated()
    {
        var @event = new Event { Id = 1, Name = "Busta Rhymes Concert", EventDate = DateTime.UtcNow.AddDays(10) };

        _eventRepository
            .Setup(x => x.CreateEventAsync(It.IsAny<Event>()))
            .ReturnsAsync(@event);

        var result = await _eventsService.CreateEventAsync(@event, UserId, CancellationToken.None);

        var createdEvent = Assert.IsType<EventCreatedResult>(result);

        _eventRepository.Verify(x => x.CreateEventAsync(It.Is<Event>(b => b.Id == @event.Id), It.IsAny<CancellationToken>()), Times.Once());
        Assert.Equal(createdEvent.Event!.Id, @event.Id);
    }

    [Fact]
    public async Task CreateEvent_WhenEventDateIsInPast_ReturnsPastEventResult()
    {
        var @event = new Event { Id = 1, Name = "Busta Rhymes Concert", EventDate = DateTime.UtcNow.AddDays(-10) };

        _eventRepository
            .Setup(x => x.CreateEventAsync(It.IsAny<Event>()))
            .ReturnsAsync(@event);

        var result = await _eventsService.CreateEventAsync(@event, UserId, CancellationToken.None);

        var createdEvent = Assert.IsType<PastEventResult>(result);

        _eventRepository.Verify(x => x.CreateEventAsync(It.Is<Event>(b => b.Id == @event.Id), It.IsAny<CancellationToken>()), Times.Never());
    }
}
