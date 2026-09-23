using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using TicketFlow.Application.Authentication.Commands.ReactivateUser;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Events;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Tests.Integration;

namespace TicketFlow.Tests.Infrastructure.Persistence;

public class AppDbContextTests
{
    [Fact]
    public async Task SaveChangesAsync_WhenEntityHasDomainEvents_PersistsOutboxMessage()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var source = new CancellationTokenSource();
        var token = source.Token;
        string? userId = null;

        // create a scope to create the inactive user
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var email = "test@gmail.com";
            var user = new User
            {
                UserName = email,
                Email = email,
                DisplayName = "Test User",
            };

            user.Deactivate();

            var createResult = await userManager.CreateAsync(user, "Test123!");
            Assert.True(createResult.Succeeded);

            userId = user.Id;

            var result = await sender.Send(new ReactivateUserCommand(email), token);
            Assert.IsType<AccountReactivated>(result);
        }

        Assert.NotNull(userId);

        // create another scope to retrieve the created user
        await using (var verificationScope = factory.Services.CreateAsyncScope())
        {
            var userManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var dbContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var persistedUser = await userManager.FindByIdAsync(userId);

            Assert.NotNull(persistedUser);
            Assert.True(persistedUser.IsActive);

            var outboxMessage = await dbContext.OutboxMessages.SingleAsync();

            Assert.Null(outboxMessage.ProcessedAt);
            Assert.Equal(typeof(UserReactivateDomainEvent).FullName, outboxMessage.Type);

            var domainEvent = JsonSerializer.Deserialize<UserReactivateDomainEvent>(outboxMessage.Payload);

            Assert.NotNull(domainEvent);
            Assert.Equal(userId, domainEvent.UserId);
        }
    }

    [Fact]
    public async Task SaveChangesAsync_WhenDomainEventsArePersisted_ClearsEventsAndDoesNotCreateDuplicateOutboxMessages()
    {
        using var source = new CancellationTokenSource();
        var token = source.Token;
        await using var factory = new CustomWebApplicationFactory();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var email = "test@gmail.com";
        var user = new User
        {
            UserName = email,
            Email = email,
            DisplayName = "Test User",
        };

        user.Deactivate();

        var createdUser = await userManager.CreateAsync(user, "Test123**90!");
        Assert.True(createdUser.Succeeded);
        Assert.Equal(EntityState.Unchanged, dbContext.Entry(user).State);

        user.Reactivate();

        Assert.Single(user.DomainEvents);

        await dbContext.SaveChangesAsync(token);

        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
        Assert.Empty(user.DomainEvents);

        await dbContext.SaveChangesAsync(token);

        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
        Assert.Empty(user.DomainEvents);
    }
}
