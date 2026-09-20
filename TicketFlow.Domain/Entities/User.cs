using Microsoft.AspNetCore.Identity;
using TicketFlow.Domain.Common;
using TicketFlow.Domain.Events;

namespace TicketFlow.Domain.Entities;

public class User : IdentityUser, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = []; 
    public string DisplayName { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public virtual ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public void Reactivate()
    {
        if (IsActive)
            return;

        IsActive = true;
        RaiseDomainEvent(new UserReactivateDomainEvent(Id));
    }

    private void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
