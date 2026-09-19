using TicketFlow.Domain.Common;

namespace TicketFlow.Domain.Events;

public sealed record BookingCreatedDomainEvent(int BookingId) : IDomainEvent;