using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Events.Commands.CreateEvent;
using TicketFlow.Application.Events.Interfaces;
using TicketFlow.Application.Events.Models;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Events.Commands;

public class CreateEventsCommandHandlerTests
{
    private readonly Mock<IEventsRepository> _eventRepository;
    private readonly Mock<ILogger<CreateEventCommandHandler>> _logger;
    private readonly CreateEventCommandHandler _handler;

    public CreateEventsCommandHandlerTests()
    {
        _eventRepository = new Mock<IEventsRepository>();
        _logger = new Mock<ILogger<CreateEventCommandHandler>>();

        _handler = new CreateEventCommandHandler(_eventRepository.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_WhenValid_ReturnsEventCreated()
    {
        var userId = Guid.NewGuid().ToString();

        var command = new CreateEventCommand("Shakira Concert", "Oxygen Arena", DateTime.UtcNow.AddDays(10), userId);

        _eventRepository
            .Setup(x => x.CreateEventAsync(
                It.IsAny<Event>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event e, CancellationToken _) => e);

        var result = await _handler.Handle(command, CancellationToken.None);

        var created = Assert.IsType<EventCreatedResult>(result);

        Assert.Equal("Shakira Concert", created.Event!.Name);
        Assert.Equal("Oxygen Arena", created.Event.Venue);

        _eventRepository.Verify(x => x.CreateEventAsync(It.Is<Event>(e => e.Name == "Shakira Concert" && e.Venue == "Oxygen Arena"), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEventDateIsInPast_ReturnsPastEventResult()
    {
        var @event = new Event { Id = 1, Name = "Busta Rhymes Concert", EventDate = DateTime.UtcNow.AddDays(-10) };
        var userId = Guid.NewGuid().ToString();
        var command = new CreateEventCommand("Busta Rhymes Concert", "London Stadium", DateTime.UtcNow.AddDays(-10), userId);

        _eventRepository
            .Setup(x => x.CreateEventAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event e, CancellationToken ct) => e);

        var result = await _handler.Handle(command, CancellationToken.None);

        var createdEvent = Assert.IsType<PastEventResult>(result);

        _eventRepository.Verify(x => x.CreateEventAsync(It.Is<Event>(b => b.Id == @event.Id ), CancellationToken.None), Times.Never());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepository()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var @event = new Event
        {
            Name = "Busta Rhymes Concert",
            EventDate = DateTime.UtcNow.AddDays(10)
        };

        var command = new CreateEventCommand("Busta Rhymes Concert", "London Stadium", DateTime.UtcNow.AddDays(10), Guid.NewGuid().ToString());

        _eventRepository
            .Setup(x => x.CreateEventAsync(@event, token))
            .ReturnsAsync((Event e, CancellationToken _) => e);

        await _handler.Handle(command, token);

        _eventRepository.Verify(x => x.CreateEventAsync(It.IsAny<Event>(), token), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_PropagatesException()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var message = "something unexpected happened";
        var exception = new InvalidOperationException(message);

        var @event = new Event
        {
            Name = "Busta Rhymes Concert",
            EventDate = DateTime.UtcNow.AddDays(10)
        };

        var command = new CreateEventCommand("Busta Rhymes Concert", "London Stadium", DateTime.UtcNow.AddDays(10), Guid.NewGuid().ToString());

        _eventRepository
            .Setup(x => x.CreateEventAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, token));

        _eventRepository.Verify(x => x.CreateEventAsync(It.Is<Event>(e => e.Name == "Busta Rhymes Concert" && e.Venue == "London Stadium"), token), Times.Once);
        Assert.Equal(message, error.Message);
    }
}
