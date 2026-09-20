using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Events;

namespace TicketFlow.Tests.Domain.Enitites;

public class UserTests
{
    [Fact]
    public async Task Reactivate_WhenUserIsInactive_ActivatesUserAndRaisesUserReactivatedDomainEvent()
    {
        var user = new User { Id = "someId", IsActive = false, Email = "test@email.com" };

        user.Reactivate();

        Assert.True(user.IsActive);

        var domainEvent = Assert.Single(user.DomainEvents);
        var userReactivatedDomainEvent = Assert.IsType<UserReactivateDomainEvent>(domainEvent);

        Assert.Equal(user.Id, userReactivatedDomainEvent.UserId);
    }
}
