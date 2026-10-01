using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using TicketFlow.Application.Abstractions.Messaging;
using TicketFlow.Application.ApplicationEvents;
using TicketFlow.Domain.Common;
using TicketFlow.Domain.Events;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Persistence.Outbox;
using TicketFlow.Tests.Integration.Infrastructure;

namespace TicketFlow.Tests.Integration.Persistence;

public class OutboxBackgroundServiceTests(IntegrationTestFixture sqlServer) : IntegrationTestsBase(sqlServer)
{
    [Fact]
    public async Task ExecuteAsync_WhenOutboxMessageExists_ProcessesMessage()
    {
        var messageDispatched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publisher = new Mock<IIntegrationEventPublisher>();
        var dispatcher = new Mock<IDomainEventDispatcher>();

        dispatcher
            .Setup(x => x.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback(() => messageDispatched.TrySetResult())
            .Returns(Task.CompletedTask);

        await using var baseFactory = CreateFactory();

        await using var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                   
                        services.RemoveAll<IDomainEventDispatcher>();
                        services.AddScoped(_ => dispatcher.Object);

                        services.RemoveAll<IIntegrationEventPublisher>();
                        services.AddScoped(_ => publisher.Object);
                });
            });

        await using (var createMessageScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = createMessageScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var domainEvent = new UserReactivateDomainEvent("user-123");
            var message = new OutboxMessage(domainEvent.GetType().FullName!, JsonSerializer.Serialize(domainEvent), OutboxMessageType.DomainEvent);

            dbContext.OutboxMessages.Add(message);

            await dbContext.SaveChangesAsync();
        }

        var scopeFactory = factory.Services.GetRequiredService<IServiceScopeFactory>();
        var options = factory.Services.GetRequiredService<IOptions<OutboxOptions>>();
        var logger = factory.Services.GetRequiredService<ILogger<OutboxBackgroundService>>();

        var worker = new OutboxBackgroundService(scopeFactory, options, logger);

        await worker.StartAsync(CancellationToken.None);
        await messageDispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        dispatcher.Verify(x => x.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
