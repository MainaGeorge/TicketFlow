using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using TicketFlow.Application.ApplicationEvents;
using TicketFlow.Application.Authentication.Commands.ReactivateUser;
using TicketFlow.Application.Authentication.Models;
using TicketFlow.Domain.Common;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Events;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Tests.Integration;

namespace TicketFlow.Tests.Infrastructure.Persistence;

public class AppDbContextTests
{
    [Fact]
    public async Task SaveChangesAsync_WhenEntityHasDomainEvents_DispatchesDomainEvents()
    {
        var dispatcher = new Mock<IDomainEventDispatcher>();
        List<IDomainEvent> dispatchedEvents = [];

        dispatcher
            .Setup(x => x.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<IDomainEvent>, CancellationToken>((domainEvents, _) => 
            {
                dispatchedEvents = [.. domainEvents];
            })
            .Returns(Task.CompletedTask);

        var factory = new CustomWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IDomainEventDispatcher>();
                    services.AddScoped(_ => dispatcher.Object);
                });
            });

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
                IsActive = false
            };

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

            var persistedUser = await userManager.FindByIdAsync(userId);

            Assert.NotNull(persistedUser);
            Assert.True(persistedUser.IsActive);
        }

        var raisedDomainEvent = Assert.IsType<UserReactivateDomainEvent>(Assert.Single(dispatchedEvents));
        Assert.Equal(userId, raisedDomainEvent.UserId);

        dispatcher.Verify(x => x.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenDomainEventsAreDispatched_ClearsEventsAndDoesNotRedispatch()
    {
        using var source = new CancellationTokenSource();
        var token = source.Token;
        var dispatcher = new Mock<IDomainEventDispatcher>();
        var factory = new CustomWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IDomainEventDispatcher>();
                    services.AddScoped(_ => dispatcher.Object);
                });
            });

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var email = "test@gmail.com";
        var user = new User
        {
            UserName = email,
            Email = email,
            DisplayName = "Test User",
            IsActive = false
        };

        var createdUser = await userManager.CreateAsync(user, "Test123**90!");
        Assert.True(createdUser.Succeeded);
        Assert.Equal(EntityState.Unchanged, dbContext.Entry(user).State);

        user.Reactivate();

        Assert.Single(user.DomainEvents);

        await dbContext.SaveChangesAsync(token);
        Assert.Empty(user.DomainEvents);

        await dbContext.SaveChangesAsync(token);

        dispatcher.Verify(x => x.DispatchAsync(It.IsAny<IEnumerable<IDomainEvent>>(), token), Times.Once);
    }
}
