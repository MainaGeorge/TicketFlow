using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Moq;
using System.Text.Json;
using TicketFlow.Application.ApplicationEvents;
using TicketFlow.Application.Messaging;
using TicketFlow.Contracts.IntegrationEvents;
using TicketFlow.Domain.Common;
using TicketFlow.Domain.Events;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Persistence.Outbox;
using TicketFlow.Tests.Integration;

namespace TicketFlow.Tests.Infrastructure.Persistence;

public class OutboxProcessorTests
{
    [Fact]
    public async Task ProcessAsync_WhenMessageExists_DispatchesEventAndPersistsOutboxMessageProcessedAt()
    {
        var dispatcher = new Mock<IDomainEventDispatcher>();
        var integrationEventPublisher = new Mock<IIntegrationEventPublisher>();
        IReadOnlyCollection<IDomainEvent> domainEvents = [];

        dispatcher
            .Setup(x => x.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<IDomainEvent> events, CancellationToken ct) => domainEvents = [.. events])
            .Returns(Task.CompletedTask);

        await using var baseFactory = new CustomWebApplicationFactory();

        await using var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IDomainEventDispatcher>();
                    services.AddScoped(_ => dispatcher.Object);

                    services.RemoveAll<IIntegrationEventPublisher>();
                    services.AddScoped(_ => integrationEventPublisher.Object);

                    var outboxHostedService = services
                            .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OutboxBackgroundService));

                    if (outboxHostedService is not null)
                    {
                        services.Remove(outboxHostedService);
                    }
                });
            });

        // create an outbox message in a scope
        await using (var createOutboxMessageScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = createOutboxMessageScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var domainEvent = new UserReactivateDomainEvent("user-123");
            var message = new OutboxMessage(domainEvent.GetType().FullName!, JsonSerializer.Serialize(domainEvent), OutboxMessageType.DomainEvent);

            dbContext.OutboxMessages.Add(message);
            await dbContext.SaveChangesAsync();

            var savedMessage = Assert.Single(dbContext.OutboxMessages);
            Assert.Null(savedMessage.ProcessedAt);
        }

        // process the outbox message created in another scope
        await using (var executeOutboxProcessorScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var registeredDispatcher = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
            var registeredIntegratedEventPublisher = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            var logger = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<ILogger<OutboxProcessor>>();
            var outboxProcessorOptions = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IOptions<OutboxOptions>>();
            var processor = new OutboxProcessor(dbContext, registeredDispatcher, registeredIntegratedEventPublisher, logger, outboxProcessorOptions);

            await processor.ProcessAsync(CancellationToken.None);

            var domainEventProcessed = Assert.Single(domainEvents);
            var runtimeTypeForProcessedEvent = Assert.IsType<UserReactivateDomainEvent>(domainEventProcessed);
            Assert.Equal("user-123", runtimeTypeForProcessedEvent.UserId);

            dispatcher.Verify(x => x.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), CancellationToken.None), Times.Once);
        }

        // verify the outbox updated the processed at in a different scope
        await using (var readDbContextScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = readDbContextScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var message = Assert.Single(dbContext.OutboxMessages);
            Assert.NotNull(message.ProcessedAt);
        }
    }

    [Fact]
    public async Task ProcessAsync_WhenDispatchFails_IncrementsRetryCountAndLeavesMessagePending()
    {
        var expectedException = new InvalidOperationException("Dispatch failed");
        var dispatcher = new Mock<IDomainEventDispatcher>();
        var integrationEventPublisher = new Mock<IIntegrationEventPublisher>();

        dispatcher
            .Setup(x => x.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        await using var baseFactory = new CustomWebApplicationFactory();

        await using var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IDomainEventDispatcher>();
                    services.AddScoped(_ => dispatcher.Object);

                    services.RemoveAll<IIntegrationEventPublisher>();
                    services.AddScoped(_ => integrationEventPublisher.Object);

                    var outboxHostedService = services
                        .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OutboxBackgroundService));

                    if (outboxHostedService is not null)
                    {
                        services.Remove(outboxHostedService);
                    }
                });
            });

        await using (var createOutboxMessageScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = createOutboxMessageScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var domainEvent = new UserReactivateDomainEvent("user-123");
            var message = new OutboxMessage(domainEvent.GetType().FullName!, JsonSerializer.Serialize(domainEvent), OutboxMessageType.DomainEvent);

            dbContext.OutboxMessages.Add(message);
            await dbContext.SaveChangesAsync();

            var savedMessage = Assert.Single(dbContext.OutboxMessages);
            Assert.Null(savedMessage.ProcessedAt);
        }

        await using (var executeOutboxProcessorScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var registeredDispatcher = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
            var logger = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<ILogger<OutboxProcessor>>();
            var outboxProcessorOptions = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IOptions<OutboxOptions>>();
            var registeredIntegratedEventPublisher = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            var processor = new OutboxProcessor(dbContext, registeredDispatcher, registeredIntegratedEventPublisher, logger, outboxProcessorOptions);

            await processor.ProcessAsync(CancellationToken.None);
            dispatcher.Verify(x => x.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), CancellationToken.None), Times.Once);
        }

        await using (var readDbContextScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = readDbContextScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var savedMessage = Assert.Single(dbContext.OutboxMessages);
            Assert.Null(savedMessage.ProcessedAt);
            Assert.Equal(1, savedMessage.RetryCount);
        }
    }

    [Fact]
    public async Task ProcessAsync_WhenSecondMessageDispatchFails_ContinuesProcessingRemainingMessages()
    {
        var expectedException = new InvalidOperationException("Dispatch failed");
        var dispatcher = new Mock<IDomainEventDispatcher>();
        var integrationEventPublisher = new Mock<IIntegrationEventPublisher>();
        var dispatchedEvents = new List<UserReactivateDomainEvent>();
        var dispatchCount = 0;
        Guid firstId;
        Guid secondId;
        Guid thirdId;

        dispatcher
            .Setup(x => x.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback
            (
                (IEnumerable<IDomainEvent> events, CancellationToken _) =>
                {
                    dispatchedEvents.Add(Assert.IsType<UserReactivateDomainEvent>(Assert.Single(events)));
                }
            )
            .Returns
            (
                () =>
                {
                    dispatchCount++;

                    return dispatchCount == 2
                        ? Task.FromException(expectedException)
                        : Task.CompletedTask;
                }
            );

        await using var baseFactory = new CustomWebApplicationFactory();

        await using var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IDomainEventDispatcher>();
                    services.AddScoped(_ => dispatcher.Object);

                    services.RemoveAll<IIntegrationEventPublisher>();
                    services.AddScoped(_ => integrationEventPublisher.Object);

                    var outboxHostedService = services
                        .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OutboxBackgroundService));

                    if (outboxHostedService is not null)
                    {
                        services.Remove(outboxHostedService);
                    }
                });
            });

        // add 3 outbox messages
        await using (var createOutboxMessageScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = createOutboxMessageScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var domainEventList = new List<UserReactivateDomainEvent> { new("user-123"), new("user-456"), new("user-789") };
            var messages = domainEventList.Select(c => new OutboxMessage(c.GetType().FullName!, JsonSerializer.Serialize(c), OutboxMessageType.DomainEvent)).ToList();

            dbContext.OutboxMessages.AddRange(messages);
            await dbContext.SaveChangesAsync();

            var savedMessages = await dbContext.OutboxMessages.OrderBy(p => p.OccurredAt).ThenBy(p => p.Id).ToListAsync();
            firstId = savedMessages[0].Id;
            secondId = savedMessages[1].Id;
            thirdId = savedMessages[2].Id;

            Assert.True(savedMessages.All(c => !c.ProcessedAt.HasValue));
        }

        // as the mock behaviour, oldest one processes, second one errors
        await using (var executeOutboxProcessorScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var registeredDispatcher = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
            var logger = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<ILogger<OutboxProcessor>>();
            var outboxProcessorOptions = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IOptions<OutboxOptions>>();
            var registeredIntegratedEventPublisher = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            var processor = new OutboxProcessor(dbContext, registeredDispatcher, registeredIntegratedEventPublisher, logger, outboxProcessorOptions);

            await processor.ProcessAsync(CancellationToken.None);
            Assert.Equal(3, dispatchedEvents.Count);

            dispatcher.Verify(x => x.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), CancellationToken.None), Times.Exactly(3));
        }

        await using (var readDbContextScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = readDbContextScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var firstMessage = await dbContext.OutboxMessages.SingleAsync(c => c.Id == firstId);
            var secondMessage = await dbContext.OutboxMessages.SingleAsync(c => c.Id == secondId);
            var thirdMessage = await dbContext.OutboxMessages.SingleAsync(c => c.Id == thirdId);

            Assert.NotNull(firstMessage.ProcessedAt);
            Assert.Equal(0, firstMessage.RetryCount);

            Assert.NotNull(thirdMessage.ProcessedAt);
            Assert.Equal(0, thirdMessage.RetryCount);

            Assert.Null(secondMessage.ProcessedAt);
            Assert.Equal(1, secondMessage.RetryCount);
        }
    }

    [Fact]
    public async Task ProcessAsync_WhenMessageReachesMaximumRetries_MarksMessageAsFailedAndStopsRetrying()
    {
        var maxAttempts = 3;
        var expectedException = new InvalidOperationException("Dispatch failed");
        var dispatcher = new Mock<IDomainEventDispatcher>();
        var integrationEventPublisher = new Mock<IIntegrationEventPublisher>();

        dispatcher
            .Setup(x => x.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        await using var baseFactory = new CustomWebApplicationFactory();

        await using var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IDomainEventDispatcher>();
                    services.AddScoped(_ => dispatcher.Object);

                    services.RemoveAll<IIntegrationEventPublisher>();
                    services.AddScoped(_ => integrationEventPublisher.Object);

                    // we need to remove the hosted service, so for testing only our manully instantiated processor does the processing
                    var outboxHostedService = services
                        .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OutboxBackgroundService));

                    if (outboxHostedService is not null)
                    {
                        services.Remove(outboxHostedService);
                    }

                    services.Configure<OutboxOptions>(options =>
                    {
                        options.MaxRetryAttempts = maxAttempts;
                    });
                });
            });

        await using (var createOutboxMessageScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = createOutboxMessageScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var domainEvent = new UserReactivateDomainEvent("user-123");
            var message = new OutboxMessage(domainEvent.GetType().FullName!, JsonSerializer.Serialize(domainEvent), OutboxMessageType.DomainEvent);

            dbContext.OutboxMessages.Add(message);
            await dbContext.SaveChangesAsync();

            var savedMessage = Assert.Single(dbContext.OutboxMessages);
            Assert.Null(savedMessage.ProcessedAt);
            Assert.Null(savedMessage.FailedAt);
        }

        for (var i = 1; i <= maxAttempts; i++)
        {
            await using (var executeOutboxProcessorScope = factory.Services.CreateAsyncScope())
            {
                var dbContext = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var registeredDispatcher = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
                var logger = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<ILogger<OutboxProcessor>>();
                var outboxProcessorOptions = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IOptions<OutboxOptions>>();
                var registeredIntegratedEventPublisher = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
                var processor = new OutboxProcessor(dbContext, registeredDispatcher, registeredIntegratedEventPublisher, logger, outboxProcessorOptions);

                await processor.ProcessAsync(CancellationToken.None);
            }

            await using (var readDbContextScope = factory.Services.CreateAsyncScope())
            {
                var dbContext = readDbContextScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var savedMessage = Assert.Single(dbContext.OutboxMessages);
                Assert.Null(savedMessage.ProcessedAt);
                Assert.Equal(i, savedMessage.RetryCount);

                if (i >= maxAttempts)
                    Assert.NotNull(savedMessage.FailedAt);
                else
                    Assert.Null(savedMessage.FailedAt);
            }
        }

        // lets do one more process, since the message is marked as failed, we should have only 3 IDispatch calls
        await using (var executeOutboxProcessorScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var registeredDispatcher = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
            var logger = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<ILogger<OutboxProcessor>>();
            var outboxProcessorOptions = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IOptions<OutboxOptions>>();
            var registeredIntegratedEventPublisher = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            var processor = new OutboxProcessor(dbContext, registeredDispatcher, registeredIntegratedEventPublisher, logger, outboxProcessorOptions);

            await processor.ProcessAsync(CancellationToken.None);
        }

        dispatcher.Verify(x => x.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), CancellationToken.None), Times.Exactly(maxAttempts));

        // one last scope to read that after the extra process runs, nothing has changed with the message
        await using (var readDbContextScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = readDbContextScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var savedMessage = Assert.Single(dbContext.OutboxMessages);

            Assert.Null(savedMessage.ProcessedAt);
            Assert.Equal(maxAttempts, savedMessage.RetryCount);
            Assert.NotNull(savedMessage.FailedAt);
        }
    }

    [Fact]
    public async Task ProcessAsync_WhenCancellationIsRequested_PropagatesCancellationWithoutRecordingFailure()
    {
        var dispatcher = new Mock<IDomainEventDispatcher>();
        var integrationEventPublisher = new Mock<IIntegrationEventPublisher>();
        using var cancellationSource = new CancellationTokenSource();
        var token = cancellationSource.Token;

        dispatcher
            .Setup(x => x.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), token))
            .Returns(() =>
            {
                cancellationSource.Cancel();
                return Task.FromCanceled(token);
            });

        await using var baseFactory = new CustomWebApplicationFactory();

        await using var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IDomainEventDispatcher>();
                    services.AddScoped(_ => dispatcher.Object);

                    services.RemoveAll<IIntegrationEventPublisher>();
                    services.AddScoped(_ => integrationEventPublisher.Object);

                    var outboxHostedService = services
                        .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OutboxBackgroundService));

                    if (outboxHostedService is not null)
                    {
                        services.Remove(outboxHostedService);
                    }
                });
            });

        await using (var createOutboxMessageScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = createOutboxMessageScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var domainEvent = new UserReactivateDomainEvent("user-123");
            var message = new OutboxMessage(domainEvent.GetType().FullName!, JsonSerializer.Serialize(domainEvent), OutboxMessageType.DomainEvent);

            dbContext.OutboxMessages.Add(message);
            await dbContext.SaveChangesAsync();

            var savedMessage = Assert.Single(dbContext.OutboxMessages);
            Assert.Null(savedMessage.ProcessedAt);
            Assert.Null(savedMessage.FailedAt);
        }

        await using (var executeOutboxProcessorScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var registeredDispatcher = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
            var logger = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<ILogger<OutboxProcessor>>();
            var outboxProcessorOptions = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IOptions<OutboxOptions>>();
            var registeredIntegratedEventPublisher = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            var processor = new OutboxProcessor(dbContext, registeredDispatcher, registeredIntegratedEventPublisher, logger, outboxProcessorOptions);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ProcessAsync(token));
        }

        await using (var readDbContextScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = readDbContextScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var savedMessage = Assert.Single(dbContext.OutboxMessages);

            Assert.Null(savedMessage.ProcessedAt);
            Assert.Null(savedMessage.FailedAt);
            Assert.Equal(0, savedMessage.RetryCount);
        }

        dispatcher.Verify(x => x.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), token), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenMessageIsIntegrationEvent_PublishesIntegrationEventAndMarksMessageProcessed()
    {
        var integrationEventsProcessor = new Mock<IIntegrationEventPublisher>();
        var userId = "user-id";
        var bookingId = 1;
        BookingCreatedIntegrationEvent? publishedEvent = null;

        integrationEventsProcessor
            .Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((message, _) =>
            {
                publishedEvent = Assert.IsType<BookingCreatedIntegrationEvent>(message);
            })
            .Returns(Task.CompletedTask);

        await using var baseFactory = new CustomWebApplicationFactory();

        await using var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IIntegrationEventPublisher>();
                    services.AddScoped(_ => integrationEventsProcessor.Object);

                    var outboxHostedService = services
                        .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OutboxBackgroundService));

                    if (outboxHostedService is not null)
                    {
                        services.Remove(outboxHostedService);
                    }
                });
            });

        await using (var createOutboxMessageScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = createOutboxMessageScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var integrationEvent = new BookingCreatedIntegrationEvent(bookingId, userId);
            var message = new OutboxMessage(integrationEvent.GetType().FullName!, JsonSerializer.Serialize(integrationEvent), OutboxMessageType.IntegrationEvent);

            dbContext.OutboxMessages.Add(message);
            await dbContext.SaveChangesAsync();

            var savedMessage = Assert.Single(dbContext.OutboxMessages);
            Assert.Null(savedMessage.ProcessedAt);
        }

        await using (var executeOutboxProcessorScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var registeredDispatcher = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
            var logger = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<ILogger<OutboxProcessor>>();
            var outboxProcessorOptions = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IOptions<OutboxOptions>>();
            var registeredIntegratedEventPublisher = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            var processor = new OutboxProcessor(dbContext, registeredDispatcher, registeredIntegratedEventPublisher, logger, outboxProcessorOptions);

            await processor.ProcessAsync(CancellationToken.None);
            integrationEventsProcessor.Verify(x => x.PublishAsync(It.IsAny<BookingCreatedIntegrationEvent>(), CancellationToken.None), Times.Once);

            Assert.NotNull(publishedEvent);
            Assert.Equal(bookingId, publishedEvent.BookingId);
            Assert.Equal(userId, publishedEvent.UserId);
        }

        await using (var verificationScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var message = Assert.Single(dbContext.OutboxMessages);

            Assert.NotNull(message.ProcessedAt);
            Assert.Equal(0, message.RetryCount);
            Assert.Null(message.FailedAt);
        }
    }

    [Fact]
    public async Task ProcessAsync_WhenIntegrationEventPublishingFails_IncrementsRetryCountAndLeavesMessagePending()
    {
        var integrationEventsProcessor = new Mock<IIntegrationEventPublisher>();
        var userId = "user-id";
        var bookingId = 1;
        var integrationEvent = new BookingCreatedIntegrationEvent(bookingId, userId);
        var expectedException = new InvalidOperationException("Integration event publishing failed");

        integrationEventsProcessor
            .Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        await using var baseFactory = new CustomWebApplicationFactory();

        await using var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IIntegrationEventPublisher>();
                    services.AddScoped(_ => integrationEventsProcessor.Object);

                    var outboxHostedService = services
                        .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OutboxBackgroundService));

                    if (outboxHostedService is not null)
                    {
                        services.Remove(outboxHostedService);
                    }
                });
            });

        await using (var createOutboxMessageScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = createOutboxMessageScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var message = new OutboxMessage(integrationEvent.GetType().FullName!, JsonSerializer.Serialize(integrationEvent), OutboxMessageType.IntegrationEvent);

            dbContext.OutboxMessages.Add(message);
            await dbContext.SaveChangesAsync();

            var savedMessage = Assert.Single(dbContext.OutboxMessages);
            Assert.Null(savedMessage.ProcessedAt);
        }

        await using (var executeOutboxProcessorScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var registeredDispatcher = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
            var logger = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<ILogger<OutboxProcessor>>();
            var outboxProcessorOptions = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IOptions<OutboxOptions>>();
            var registeredIntegratedEventPublisher = executeOutboxProcessorScope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

            var processor = new OutboxProcessor(dbContext, registeredDispatcher, registeredIntegratedEventPublisher, logger, outboxProcessorOptions);

            await  processor.ProcessAsync(CancellationToken.None);
            integrationEventsProcessor.Verify(x => x.PublishAsync(It.IsAny<BookingCreatedIntegrationEvent>(), CancellationToken.None), Times.Once);
        }

        await using (var verificationScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var message = Assert.Single(dbContext.OutboxMessages);

            Assert.Null(message.ProcessedAt);
            Assert.Equal(1, message.RetryCount);
            Assert.Null(message.FailedAt);
        }
    }
}
