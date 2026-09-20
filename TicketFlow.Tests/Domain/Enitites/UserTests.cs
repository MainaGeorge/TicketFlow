using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Events;

namespace TicketFlow.Tests.Domain.Enitites;

public class UserTests
{
    [Fact]
    public void Reactivate_WhenUserIsInactive_ActivatesUserAndRaisesUserReactivatedDomainEvent()
    {
        var user = new User
        {
            Id = "someId",
            Email = "test@email.com"
        };

        user.Deactivate();
        Assert.False(user.IsActive);

        user.Reactivate();

        Assert.True(user.IsActive);

        var domainEvent = Assert.Single(user.DomainEvents);
        var userReactivatedDomainEvent = Assert.IsType<UserReactivateDomainEvent>(domainEvent);
        Assert.Equal(user.Id, userReactivatedDomainEvent.UserId);
    }

    [Fact]
    public void Deactivate_WhenUserIsActive_DeactivatesUser()
    {
        var user = new User
        {
            Id = "someId",
            Email = "test@email.com"
        };

        Assert.True(user.IsActive);

        user.Deactivate();

        Assert.False(user.IsActive);
    }
}