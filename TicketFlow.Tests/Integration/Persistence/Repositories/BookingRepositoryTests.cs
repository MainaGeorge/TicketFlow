using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using TicketFlow.Application.Bookings.Exceptions;
using TicketFlow.Contracts.IntegrationEvents;
using TicketFlow.Domain.Entities;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Persistence.Outbox;
using TicketFlow.Infrastructure.Persistence.Repositories;

namespace TicketFlow.Tests.Integration.Persistence.Repositories;

public class BookingRepositoryTests
{
    [Fact]
    public async Task SaveCreatedBookingAsync_WhenBookingIsValid_PersistsBookingAndIntegrationEventOutboxMessage()
    {
        var email = "test@user.com";

        await using var factory = new CustomWebApplicationFactory();
        await using (var createEventAndSeatScope = factory.Services.CreateAsyncScope())
        {
            var context = createEventAndSeatScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = createEventAndSeatScope.ServiceProvider.GetRequiredService<UserManager<User>>();

            var @event = new Event { Name = "Shakira Concert", EventDate = DateTime.UtcNow.AddDays(10), Venue = "London Stadium" };
            var seat = new Seat { Event = @event, Number = 20, Price = 100m, Row = "A" };

            var userCreationResult = await userManager.CreateAsync(new User { Email = email, DisplayName = "Test", UserName = "James" }, "tesTpassword123!");
            Assert.True(userCreationResult.Succeeded);

            context.Events.Add(@event);
            context.Seats.Add(seat);

            await context.SaveChangesAsync();
        }

        await using (var createBookingScope = factory.Services.CreateAsyncScope()) 
        { 
            var dbContext = createBookingScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = createBookingScope.ServiceProvider.GetRequiredService<UserManager<User>>();

            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);

            var createdSeat = Assert.Single(dbContext.Seats);
            var seatId = createdSeat.Id;

            var repository = new BookingRepository(dbContext);
            var booking = Booking.Create(user.Id, seatId, DateTime.UtcNow);

            await repository.SaveCreatedBookingAsync(booking, CancellationToken.None);
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var databaseContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var createdBooking = Assert.Single(databaseContext.Bookings);
        var outboxMessage = Assert.Single(databaseContext.OutboxMessages);
        var integrationEvent = JsonSerializer.Deserialize<BookingCreatedIntegrationEvent>(outboxMessage.Payload);

        Assert.Equal(OutboxMessageType.IntegrationEvent, outboxMessage.MessageType);
        Assert.Equal(typeof(BookingCreatedIntegrationEvent).FullName, outboxMessage.Type);
        Assert.NotNull(integrationEvent);
        Assert.Equal(createdBooking.Id, integrationEvent.BookingId);
        Assert.Equal(createdBooking.UserId, integrationEvent.UserId);
    }

    [Fact]
    public async Task SaveCreatedBookingAsync_WhenSeatIsAlreadyBooked_ThrowsSeatAlreadyBookedException()
    {
        var email = "test@user.com";
        await using var factory = new CustomWebApplicationFactory();

        await using (var createEventAndSeatScope = factory.Services.CreateAsyncScope())
        {
            var context = createEventAndSeatScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = createEventAndSeatScope.ServiceProvider.GetRequiredService<UserManager<User>>();

            var @event = new Event { Name = "Shakira Concert", EventDate = DateTime.UtcNow.AddDays(10), Venue = "London Stadium" };
            var seat = new Seat { Event = @event, Number = 20, Price = 100m, Row = "A" };

            var userCreationResult = await userManager.CreateAsync(new User { Email = email, DisplayName = "Test", UserName = "James" }, "tesTpassword123!");
            Assert.True(userCreationResult.Succeeded);

            context.Events.Add(@event);
            context.Seats.Add(seat);

            await context.SaveChangesAsync();
        }

        await using (var createBookingScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = createBookingScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = createBookingScope.ServiceProvider.GetRequiredService<UserManager<User>>();

            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);

            var createdSeat = Assert.Single(dbContext.Seats);
            var seatId = createdSeat.Id;

            var repository = new BookingRepository(dbContext);
            var booking = Booking.Create(user.Id, seatId, DateTime.UtcNow);

            await repository.SaveCreatedBookingAsync(booking, CancellationToken.None);
        }

        await using (var duplicateBookingScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = duplicateBookingScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = duplicateBookingScope.ServiceProvider.GetRequiredService<UserManager<User>>();

            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);

            var createdSeat = Assert.Single(dbContext.Seats);
            var seatId = createdSeat.Id;

            var booking = Booking.Create(user.Id, seatId, DateTime.UtcNow);
            var repository = new BookingRepository(dbContext);

            var exception = await Assert.ThrowsAsync<SeatAlreadyBookedException>(() => repository.SaveCreatedBookingAsync(booking));
            Assert.Equal(createdSeat.Id, exception.SeatId);
        }

        await using (var verificationScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var booking = Assert.Single(dbContext.Bookings);
            var outboxMessage = Assert.Single(dbContext.OutboxMessages);
            var integrationEvent = JsonSerializer.Deserialize<BookingCreatedIntegrationEvent>(outboxMessage.Payload);

            Assert.NotNull(integrationEvent);
            Assert.Equal(booking.Id, integrationEvent.BookingId);
        }
    }

    [Fact]
    public async Task GetBookingAsync_WhenBookingBelongsToDifferentUser_ReturnsNull()
    {
        var email = "test@user.com";
        var email2 = "test2@user.com";

        await using var factory = new CustomWebApplicationFactory();

        await using (var createEventAndSeatScope = factory.Services.CreateAsyncScope())
        {
            var context = createEventAndSeatScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = createEventAndSeatScope.ServiceProvider.GetRequiredService<UserManager<User>>();

            var @event = new Event { Name = "Boxing Event", EventDate = DateTime.UtcNow.AddDays(10), Venue = "London Stadium" };
            var seat = new Seat { Event = @event, Number = 20, Price = 100m, Row = "A" };

            var userCreationResult = await userManager.CreateAsync(new User { Email = email, DisplayName = "Test", UserName = "James" }, "tesTpassword123!");
            var user2CreationResult = await userManager.CreateAsync(new User { Email = email2, DisplayName = "Test", UserName = "John" }, "tesTpassword123!");

            Assert.True(userCreationResult.Succeeded);
            Assert.True(user2CreationResult.Succeeded);

            context.Events.Add(@event);
            context.Seats.Add(seat);

            await context.SaveChangesAsync();
        }

        await using (var createBookingScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = createBookingScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = createBookingScope.ServiceProvider.GetRequiredService<UserManager<User>>();

            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);

            var createdSeat = Assert.Single(dbContext.Seats);
            var seatId = createdSeat.Id;

            var repository = new BookingRepository(dbContext);
            var booking = Booking.Create(user.Id, seatId, DateTime.UtcNow);

            await repository.SaveCreatedBookingAsync(booking, CancellationToken.None);
        }

        await using (var verificationScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var repository = new BookingRepository(dbContext);
            var userManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByEmailAsync(email2);
            var retrievedBooking = Assert.Single(dbContext.Bookings);

            Assert.NotNull(user);
            var booking = await repository.GetBookingAsync(retrievedBooking.Id, user.Id);
            Assert.Null(booking);
        }
    }

    [Fact]
    public async Task GetSeatForBookingAsync_WhenSeatBelongsToDifferentEvent_ReturnsNull()
    {
        var wrongEventName = "Black Friday Sales";
        await using var factory = new CustomWebApplicationFactory();

        await using (var createEventAndSeatScope = factory.Services.CreateAsyncScope())
        {
            var context = createEventAndSeatScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var @event = new Event { Name ="Shakira Concert", EventDate = DateTime.UtcNow.AddDays(10), Venue = "London Stadium" };
            var @event2 = new Event { Name = wrongEventName, EventDate = DateTime.UtcNow.AddDays(10), Venue = "Emirates Stadium" };
            var seat = new Seat { Event = @event, Number = 20, Price = 100m, Row = "A" };

            context.Events.Add(@event);
            context.Events.Add(@event2);
            context.Seats.Add(seat);

            await context.SaveChangesAsync();
        }

        await using (var verificationScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var repository = new BookingRepository(dbContext);

            var seat = Assert.Single(dbContext.Seats);
            var wrongEvent = await dbContext.Events.SingleOrDefaultAsync(c => c.Name == wrongEventName);

            Assert.NotNull(wrongEvent);

            var result = await repository.GetSeatForBookingAsync(wrongEvent.Id, seat.Id);
            Assert.Null(result);
        }
    }
}
