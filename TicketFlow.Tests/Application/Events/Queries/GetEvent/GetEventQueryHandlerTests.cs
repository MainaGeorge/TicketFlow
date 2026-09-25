using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Events.Interfaces;
using TicketFlow.Application.Events.Models;
using TicketFlow.Application.Events.Queries.GetEvent;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Events.Queries.GetEvent;

public class GetEventQueryHandlerTests
{
    private readonly Mock<ILogger<GetEventQueryHandler>> _logger;
    private readonly Mock<IEventsRepository> _eventsRepository;
    private readonly Mock<ICacheService> _cacheService;
    private readonly GetEventQueryHandler _handler;

    public GetEventQueryHandlerTests()
    {
        _logger = new Mock<ILogger<GetEventQueryHandler>>();
        _eventsRepository = new Mock<IEventsRepository>();
        _cacheService = new Mock<ICacheService>();
        _handler = new GetEventQueryHandler(_eventsRepository.Object, _cacheService.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_WhenEventIsNotCachedAndExists_ReturnsEventAndCachesResult()
    {
        var eventId = 1;
        var query = new GetEventQuery(eventId);
        var cacheKey = $"event:{query.Id}";
        var @event = new Event { Id = eventId, Venue = "Emirates Stadium", Name = "FA Cup Final" };

        _cacheService
            .Setup(x => x.GetAsync<EventResult>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventResult?)null);

        _cacheService
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<EventResult>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        var result = await _handler.Handle(query, CancellationToken.None);

        var retrievedEvent = Assert.IsType<EventResult>(result);

        Assert.Equal(@event.Name, retrievedEvent.Event!.Name);
        Assert.Equal(@event.Id, retrievedEvent.Event.Id);
        Assert.Equal(@event.Venue, retrievedEvent.Event!.Venue);

        _eventsRepository.Verify(x => x.GetEventAsync(eventId, CancellationToken.None), Times.Once);
        _cacheService.Verify(x => x.GetAsync<EventResult>(cacheKey, CancellationToken.None), Times.Once);
        _cacheService.Verify(
            x => x.SetAsync(
                cacheKey, 
                It.Is<EventResult>(r => r.Event.Id == @event.Id && r.Event.Name == @event.Name && r.Event.Venue == @event.Venue),
                TimeSpan.FromMinutes(5),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEventDoesNotExist_ReturnsEventNotFoundAndDoesNotCacheAnything()
    {
        var eventId = 1;
        var query = new GetEventQuery(eventId);
        var cacheKey = $"event:{query.Id}";

        _cacheService
            .Setup(x => x.GetAsync<EventResult>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventResult?)null);

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        var retrievedEvent = Assert.IsType<EventNotFoundResult>(result);
        _eventsRepository.Verify(x => x.GetEventAsync(eventId, CancellationToken.None), Times.Once());
        _cacheService.Verify(x => x.GetAsync<EventResult>(cacheKey, CancellationToken.None), Times.Once);
        _cacheService.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<EventResult>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepositoryAndCache()
    {
        var eventId = 1;
        using var cancellationTokenSource = new CancellationTokenSource();
        var token = cancellationTokenSource.Token;
        var query = new GetEventQuery(eventId);
        var cacheKey = $"event:{query.Id}";
        var @event = new Event { Id = eventId, Venue = "Emirates Stadium", Name = "FA Cup Final" };
        var eventResult = new EventResult(@event);

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _cacheService
            .Setup(x => x.GetAsync<EventResult>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventResult?)null);

        var result = await _handler.Handle(new GetEventQuery(eventId), token);

        _eventsRepository.Verify(x => x.GetEventAsync(eventId, token), Times.Once());
        _cacheService.Verify(x => x.GetAsync<EventResult>(cacheKey, token), Times.Once);
        _cacheService.Verify(
            x => x.SetAsync(
                cacheKey,
                It.Is<EventResult>(r => r.Event.Id == @event.Id && r.Event.Name == @event.Name && r.Event.Venue == @event.Venue),
                TimeSpan.FromMinutes(5),
                token),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_PropagatesException()
    {
        var eventId = 1;
        var message = "something went wrong while querying the db";
        var exception = new Exception(message);
        var query = new GetEventQuery(eventId);
        var cacheKey = $"event:{query.Id}";

        _cacheService
            .Setup(x => x.GetAsync<EventResult>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventResult?)null);

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var error = await Assert.ThrowsAsync<Exception>(() => _handler.Handle(query, CancellationToken.None));

        Assert.Equal(message, error.Message);
        Assert.Same(exception, error);
        _eventsRepository.Verify(x => x.GetEventAsync(eventId, CancellationToken.None), Times.Once);
        _cacheService.Verify(x => x.GetAsync<EventResult>(cacheKey, CancellationToken.None), Times.Once);
        _cacheService.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<EventResult>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }


    [Fact]
    public async Task Handle_WhenEventIsCached_ReturnsCachedEventWithoutQueryingRepository()
    {
        var eventId = 1;
        var query = new GetEventQuery(eventId);
        var @event = new Event { Id = eventId, Venue = "Emirates Stadium", Name = "FA Cup Final" };
        var cacheKey = $"event:{query.Id}";

        _cacheService
            .Setup(x => x.GetAsync<EventResult>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventResult(@event));

        var result = await _handler.Handle(query, CancellationToken.None);

        var retrievedEvent = Assert.IsType<EventResult>(result);

        Assert.Equal(@event.Name, retrievedEvent.Event!.Name);
        Assert.Equal(@event.Id, retrievedEvent.Event.Id);
        Assert.Equal(@event.Venue, retrievedEvent.Event!.Venue);

        _cacheService.Verify(x => x.GetAsync<EventResult>(cacheKey, CancellationToken.None), Times.Once);
        _eventsRepository.Verify(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheService.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<EventResult>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCacheReadFails_FallsBackToRepository()
    {
        var eventId = 1;
        var query = new GetEventQuery(eventId);
        var @event = new Event { Id = eventId, Venue = "Emirates Stadium", Name = "FA Cup Final" };

        _cacheService
            .Setup(x => x.GetAsync<EventResult>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis unavailable"));

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        var result = await _handler.Handle(query, CancellationToken.None);
        var retrievedEvent = Assert.IsType<EventResult>(result);

        Assert.Equal(@event.Name, retrievedEvent.Event!.Name);
        Assert.Equal(@event.Id, retrievedEvent.Event.Id);
        Assert.Equal(@event.Venue, retrievedEvent.Event!.Venue);

        _eventsRepository.Verify(x => x.GetEventAsync(eventId, CancellationToken.None), Times.Once);
        _cacheService.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<EventResult>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCacheReadIsCancelled_PropagatesCancellation()
    {
        var query = new GetEventQuery(1);
        using var source = new CancellationTokenSource();
        var token = source.Token;
        source.Cancel();

        _cacheService
            .Setup(x => x.GetAsync<EventResult>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(token));

        await Assert.ThrowsAsync<OperationCanceledException>(() => _handler.Handle(query, token));

        _eventsRepository.Verify(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCacheWriteFails_ReturnsEvent()
    {
        var eventId = 1;
        var query = new GetEventQuery(1);
        var @event = new Event { Id = eventId, Venue = "Emirates Stadium", Name = "FA Cup Final" };
        var cacheKey = $"event:{query.Id}";

        _cacheService
            .Setup(x => x.GetAsync<EventResult>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventResult?)null);

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _cacheService
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<EventResult>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis unavailable"));

        var result = await _handler.Handle(query, CancellationToken.None);
        var retrievedEvent = Assert.IsType<EventResult>(result);

        Assert.Equal(@event.Name, retrievedEvent.Event!.Name);
        Assert.Equal(@event.Id, retrievedEvent.Event.Id);
        Assert.Equal(@event.Venue, retrievedEvent.Event!.Venue);
    }

    [Fact]
    public async Task Handle_WhenCacheWriteIsCancelled_PropagatesCancellation()
    {
        var eventId = 1;
        var query = new GetEventQuery(1);
        var @event = new Event { Id = eventId, Venue = "Emirates Stadium", Name = "FA Cup Final" };
        var cacheKey = $"event:{query.Id}";
        using var source = new CancellationTokenSource();
        var token = source.Token;

        _cacheService
            .Setup(x => x.GetAsync<EventResult>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventResult?)null);

        _eventsRepository
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        _cacheService
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<EventResult>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback((string k, EventResult e, TimeSpan s, CancellationToken c) => source.Cancel())
            .ThrowsAsync(new OperationCanceledException(token));

        await Assert.ThrowsAsync<OperationCanceledException>(() => _handler.Handle(query, token));

        _eventsRepository.Verify(x => x.GetEventAsync(eventId, token), Times.Once);
        _cacheService.Verify(x => x.SetAsync(cacheKey, It.IsAny<EventResult>(), TimeSpan.FromMinutes(5), token), Times.Once);
    }
}
