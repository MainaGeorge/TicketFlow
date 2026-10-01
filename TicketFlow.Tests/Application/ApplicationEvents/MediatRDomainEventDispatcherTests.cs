using MediatR;
using Moq;
using TicketFlow.Application.ApplicationEvents;
using TicketFlow.Domain.Common;
using TicketFlow.Domain.Events;

namespace TicketFlow.Tests.Application.ApplicationEvents;

public class MediatRDomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_WhenDomainEventProvided_PublishesDomainEventNotification()
    {
        var publisher = new Mock<IPublisher>();
        var userId = "user-id";
        var domainEvent = new UserReactivateDomainEvent(userId);
        List<IDomainEvent> domainEvents = [domainEvent];
        var mediatRDomainEventDispatcher = new MediatRDomainEventDispatcher(publisher.Object);
        object? publishedNotification = null;

        publisher
            .Setup(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback((object de, CancellationToken _) => publishedNotification = de)
            .Returns(Task.CompletedTask);

        await mediatRDomainEventDispatcher.DispatchAsync(domainEvents, CancellationToken.None);

        Assert.NotNull(publishedNotification);
        var eventNotification = Assert.IsType<DomainEventNotification<UserReactivateDomainEvent>>(publishedNotification);

        Assert.Same(domainEvent, eventNotification.DomainEvent);
        Assert.Equal(userId, eventNotification.DomainEvent.UserId);

        publisher.Verify(x => x.Publish(It.IsAny<object>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_PassesCancellationToken_ToPublisher()
    {
        var publisher = new Mock<IPublisher>();
        var (id1, id2) = ("user-id", "user-id-2");
        List<IDomainEvent> domainEvents = [new UserReactivateDomainEvent(id1), new UserReactivateDomainEvent(id2)];
        var mediatRDomainEventDispatcher = new MediatRDomainEventDispatcher(publisher.Object);
        using var source = new CancellationTokenSource();
        var token = source.Token;

        publisher
            .Setup(x => x.Publish(It.IsAny<object>(), token))
            .Returns(Task.CompletedTask);

        await mediatRDomainEventDispatcher.DispatchAsync(domainEvents, token);

        publisher.Verify(x => x.Publish(It.IsAny<object>(), token), Times.Exactly(2));
    }
}
