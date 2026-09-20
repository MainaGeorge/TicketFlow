using TicketFlow.Domain.Common;

namespace TicketFlow.Domain.Events;

public sealed record UserReactivateDomainEvent(string UserId) : IDomainEvent;
