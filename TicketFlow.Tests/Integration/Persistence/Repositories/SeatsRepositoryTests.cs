using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketFlow.Application.Seats.Exceptions;
using TicketFlow.Domain.Entities;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Persistence.Outbox;
using TicketFlow.Infrastructure.Persistence.Repositories;

namespace TicketFlow.Tests.Integration.Persistence.Repositories;

public class SeatsRepositoryTests
{
    [Fact]
    public async Task CreateSeatsAsync_WhenOneSeatIsDuplicateInPayload_DoesNotPersistAnySeats()
    {
        var seats = new List<Seat> { new() { Number = 2, Row = "C" }, new() { Number = 2, Row = "B" }, new() {  Number = 2, Row = "B" } };
        var @event = new Event { Name = "Rock Concert", Venue = "London Arena", EventDate = DateTime.UtcNow.AddDays(30) };
        int eventId;

        await using var baseFactory = new CustomWebApplicationFactory();

        await using var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var outboxHostedService = services
                            .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OutboxBackgroundService));

                    if (outboxHostedService is not null)
                    {
                        services.Remove(outboxHostedService);
                    }
                });
            });

        await using (var creationScope = factory.Services.CreateAsyncScope())
        {
            var context = creationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seatsRepository = new SeatsRepository(context);
            await context.Events.AddAsync(@event);
            await context.SaveChangesAsync();
            eventId = @event.Id;
            Assert.Single(context.Events);
        }

        await using (var anotherCreationScope = factory.Services.CreateAsyncScope())
        {
            var context = anotherCreationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seatsRepository = new SeatsRepository(context);
            seats.ForEach(c => c.EventId = eventId);
            var exception = await Assert.ThrowsAsync<DuplicateSeatException>(() => seatsRepository.CreateSeatsAsync(seats, CancellationToken.None));
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedSeats = await verificationContext.Seats.ToListAsync();

        Assert.Empty(persistedSeats);
    }

    [Fact]
    public async Task CreateSeatsAsync_WhenOneSeatIsDuplicate_DoesNotPersistAnySeats()
    {
        var firstSeat = new List<Seat> { new() { Number = 2, Row = "B" } };
        var seats = new List<Seat> { new() { EventId = 1, Number = 2, Row = "C" }, new() { EventId = 1, Number = 2, Row = "B" } };
        var @event = new Event { Name = "Rock Concert", Venue = "London Arena", EventDate = DateTime.UtcNow.AddDays(30) };
        int eventId;

        await using var baseFactory = new CustomWebApplicationFactory();

        await using var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var outboxHostedService = services
                            .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OutboxBackgroundService));

                    if (outboxHostedService is not null)
                    {
                        services.Remove(outboxHostedService);
                    }
                });
            });

        await using (var creationScope = factory.Services.CreateAsyncScope())
        {
            var context = creationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seatsRepository = new SeatsRepository(context);
            await context.Events.AddAsync(@event);
            await context.SaveChangesAsync();
            eventId = @event.Id;
            firstSeat[0].EventId = @event.Id;
            await seatsRepository.CreateSeatsAsync(firstSeat, CancellationToken.None);
            Assert.Single(context.Seats);
        }

        await using (var anotherCreationScope = factory.Services.CreateAsyncScope())
        {
            var context = anotherCreationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seatsRepository = new SeatsRepository(context);
            seats.ForEach(c => c.EventId = eventId);
            var exception = await Assert.ThrowsAsync<DuplicateSeatException>(() => seatsRepository.CreateSeatsAsync(seats, CancellationToken.None));
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedSeats = await verificationContext.Seats.ToListAsync();

        var persistedSeat = Assert.Single(persistedSeats);
        Assert.Equal(eventId, persistedSeat.EventId);
        Assert.Equal("B", persistedSeat.Row);
        Assert.Equal(2, persistedSeat.Number);
    }

    [Fact]
    public async Task CreateSeatsAsync_WithSameRowAndNumberForDifferentEvents_PersistsSuccessfully()
    {
        var event1 = new Event { Name = "Rock Concert", Venue = "London Arena", EventDate = DateTime.UtcNow.AddDays(30) };
        var event2 = new Event { Name = "Rock & Roll Concert", Venue = "Madisson Square Garden", EventDate = DateTime.UtcNow.AddDays(30) };
        int event1Id;
        int event2Id;

        await using var baseFactory = new CustomWebApplicationFactory();
        await using var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var outboxHostedService = services
                            .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OutboxBackgroundService));

                    if (outboxHostedService is not null)
                    {
                        services.Remove(outboxHostedService);
                    }
                });
            });

        await using (var creationScope = factory.Services.CreateAsyncScope())
        {
            var context = creationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seatsRepository = new SeatsRepository(context);
            await context.Events.AddAsync(event1);
            await context.Events.AddAsync(event2);
            await context.SaveChangesAsync();
            event1Id = event1.Id;
            event2Id = event2.Id;
        }

        await using (var anotherCreationScope = factory.Services.CreateAsyncScope())
        {
            var context = anotherCreationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var events = await context.Events.ToListAsync();
            var firstEventSeat = new List<Seat>
            {
                new()
                {
                    EventId = event1Id,
                    Number = 2,
                    Row = "C",
                    Price = 100m
                }
            };

            var secondEventSeat = new List<Seat>
            {
                new()
                {
                    EventId = event2Id,
                    Number = 2,
                    Row = "C",
                    Price = 100m
                }
            };

            var seatsRepository = new SeatsRepository(context);
            await seatsRepository.CreateSeatsAsync(firstEventSeat, CancellationToken.None);
            await seatsRepository.CreateSeatsAsync(secondEventSeat, CancellationToken.None);
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedSeats = await verificationContext.Seats.ToListAsync();

        Assert.Equal(2, persistedSeats.Count);
        Assert.Contains(persistedSeats, s => s.EventId == event1Id && s.Row == "C" && s.Number == 2);
        Assert.Contains(persistedSeats, s => s.EventId == event2Id && s.Row == "C" && s.Number == 2);
    }

    [Fact]
    public async Task GetSeatAsync_WhenSeatBelongsToDifferentEvent_ReturnsNull()
    {
        var seats = new List<Seat> { new() { Number = 2, Row = "C" } };
        var @event = new Event { Name = "Rock Concert", Venue = "London Arena", EventDate = DateTime.UtcNow.AddDays(30) };
        var event2 = new Event { Name = "Rock Concert", Venue = "London Arena", EventDate = DateTime.UtcNow.AddDays(30) };
        int event1Id;
        int event2Id;

        await using var baseFactory = new CustomWebApplicationFactory();

        await using var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var outboxHostedService = services
                            .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OutboxBackgroundService));

                    if (outboxHostedService is not null)
                    {
                        services.Remove(outboxHostedService);
                    }
                });
            });

        await using (var creationScope = factory.Services.CreateAsyncScope())
        {
            var context = creationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Events.AddAsync(@event);
            await context.Events.AddAsync(event2);
            await context.SaveChangesAsync();
            event1Id = @event.Id;
            event2Id = event2.Id;
        }

        await using (var anotherCreationScope = factory.Services.CreateAsyncScope())
        {
            var context = anotherCreationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seatsRepository = new SeatsRepository(context);
            seats[0].EventId = event1Id;
            await seatsRepository.CreateSeatsAsync(seats, CancellationToken.None);
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedSeats = await verificationContext.Seats.ToListAsync();
        Assert.Single(persistedSeats);

        var verificationSeatRepository = new SeatsRepository(verificationContext);
        var seat = await verificationSeatRepository.GetSeatAsync(event2Id, persistedSeats.First().Id);

        Assert.Null(seat);
    }

    [Fact]
    public async Task GetSeatsAsync_ReturnsOnlySeatsForRequestedEvent()
    {
        var event1 = new Event { Name = "Rock Concert", Venue = "London Arena", EventDate = DateTime.UtcNow.AddDays(30) };
        var event2 = new Event { Name = "Rock & Roll Concert", Venue = "Madisson Square Garden", EventDate = DateTime.UtcNow.AddDays(30) };
        int event1Id;
        int event2Id;

        await using var baseFactory = new CustomWebApplicationFactory();
        await using var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var outboxHostedService = services
                            .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OutboxBackgroundService));

                    if (outboxHostedService is not null)
                    {
                        services.Remove(outboxHostedService);
                    }
                });
            });

        await using (var creationScope = factory.Services.CreateAsyncScope())
        {
            var context = creationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Events.AddAsync(event1);
            await context.Events.AddAsync(event2);
            await context.SaveChangesAsync();

            event1Id = event1.Id;
            event2Id = event2.Id;

            Assert.Equal(2, context.Events.Count());
        }

        await using (var anotherCreationScope = factory.Services.CreateAsyncScope())
        {
            var context = anotherCreationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var events = await context.Events.ToListAsync();
            var firstEventSeats = new List<Seat>
            {
                new()
                {
                    EventId = event1Id,
                    Number = 1,
                    Row = "A",
                    Price = 100m
                },
                new()
                {
                    EventId = event1Id,
                    Number = 2,
                    Row = "A",
                    Price = 100m
                }
            };

            var secondEventSeats = new List<Seat>
            {
                new()
                {
                    EventId = event2Id,
                    Number = 1,
                    Row = "B",
                    Price = 100m
                },
                new()
                {
                    EventId = event2Id,
                    Number = 2,
                    Row = "B",
                    Price = 100m
                }
            };

            var seatsRepository = new SeatsRepository(context);
            await seatsRepository.CreateSeatsAsync(firstEventSeats, CancellationToken.None);
            await seatsRepository.CreateSeatsAsync(secondEventSeats, CancellationToken.None);
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var verificationSeatsRepository = new SeatsRepository(verificationContext);
        var result = (await verificationSeatsRepository.GetSeatsAsync(event1Id, CancellationToken.None)).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, seat => Assert.Equal(event1Id, seat.EventId));
        Assert.Contains(result, seat => seat.Row == "A" && seat.Number == 1);
        Assert.Contains(result, seat => seat.Row == "A" && seat.Number == 2);
        Assert.DoesNotContain(result, seat => seat.EventId == event2Id);
    }

    [Fact]
    public async Task GetSeatAsync_WhenSeatExistsForEvent_ReturnsSeat()
    {
        var @event = new Event { Name = "Rock Concert", Venue = "London Arena", EventDate = DateTime.UtcNow.AddDays(30) };
        int eventId;

        await using var baseFactory = new CustomWebApplicationFactory();

        await using var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var outboxHostedService = services
                            .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OutboxBackgroundService));

                    if (outboxHostedService is not null)
                    {
                        services.Remove(outboxHostedService);
                    }
                });
            });

        await using (var creationScope = factory.Services.CreateAsyncScope())
        {
            var context = creationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Events.AddAsync(@event);
            await context.SaveChangesAsync();
            eventId = @event.Id;
        }

        var seats = new List<Seat>
            {
                new()
                {
                    EventId = eventId,
                    Number = 2,
                    Row = "C",
                    Price = 100m
                }
            };

        await using (var anotherCreationScope = factory.Services.CreateAsyncScope())
        {
            var context = anotherCreationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seatsRepository = new SeatsRepository(context);
            await seatsRepository.CreateSeatsAsync(seats, CancellationToken.None);
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedSeatId = await verificationContext.Seats.Select(s => s.Id).SingleAsync();
        var verificationSeatsRepository = new SeatsRepository(verificationContext);
        var seat = await verificationSeatsRepository.GetSeatAsync(eventId, persistedSeatId, CancellationToken.None);

        Assert.NotNull(seat);
        Assert.Equal(persistedSeatId, seat.Id);
        Assert.Equal(eventId, seat.EventId);
        Assert.Equal("C", seat.Row);
        Assert.Equal(2, seat.Number);
        Assert.Equal(100m, seat.Price);
    }

    [Fact]
    public async Task CreateSeatsAsync_WithValidSeats_PersistsAllSeats()
    {
        var @event = new Event { Name = "Rock Concert", Venue = "London Arena", EventDate = DateTime.UtcNow.AddDays(30) };
        int eventId;

        await using var baseFactory = new CustomWebApplicationFactory();

        await using var factory = baseFactory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var outboxHostedService = services
                            .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(OutboxBackgroundService));

                    if (outboxHostedService is not null)
                    {
                        services.Remove(outboxHostedService);
                    }
                });
            });

        await using (var creationScope = factory.Services.CreateAsyncScope())
        {
            var context = creationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Events.AddAsync(@event);
            await context.SaveChangesAsync();
            eventId = @event.Id;
        }

        var seats = new List<Seat>
            {
                new()
                {
                    EventId = eventId,
                    Number = 1,
                    Row = "C",
                    Price = 50m
                },
                 new()
                {
                    EventId = eventId,
                    Number = 2,
                    Row = "D",
                    Price = 100m
                }
            };

        await using (var anotherCreationScope = factory.Services.CreateAsyncScope())
        {
            var context = anotherCreationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seatsRepository = new SeatsRepository(context);
            await seatsRepository.CreateSeatsAsync(seats, CancellationToken.None);
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedSeats = await verificationContext.Seats.ToListAsync();

        Assert.Equal(2, persistedSeats.Count);
        Assert.True(persistedSeats.All(s => s.EventId == eventId));
        Assert.Contains(persistedSeats, f => f.Number == 1 && f.Row == "C" && f.Price == 50m);
        Assert.Contains(persistedSeats, f => f.Number == 2 && f.Row == "D" && f.Price == 100m);
    }

}
