using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using TicketFlow.Application.ApplicationEvents;
using TicketFlow.Domain.Common;
using TicketFlow.Domain.Events;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Persistence.Outbox;

namespace TicketFlow.Tests.Integration.Persistence;

public class OutboxBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenOutboxMessageExists_ProcessesMessage()
    {
        var messageDispatched = new TaskCompletionSource();
        var dispatcher = new Mock<IDomainEventDispatcher>();

        dispatcher
            .Setup(x => x.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback(() => messageDispatched.TrySetResult())
            .Returns(Task.CompletedTask);

        await using var baseFactory = new CustomWebApplicationFactory();

        await using var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IDomainEventDispatcher>();
                    services.AddScoped(_ => dispatcher.Object);

                    var outboxHostedService = services
                        .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OutboxBackgroundService));

                    if (outboxHostedService is not null)
                    {
                        services.Remove(outboxHostedService);
                    }
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
    }
}
