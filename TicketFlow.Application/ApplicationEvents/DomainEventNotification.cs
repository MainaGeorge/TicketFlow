using MediatR;
using TicketFlow.Domain.Common;

namespace TicketFlow.Application.ApplicationEvents;

public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification where TDomainEvent : IDomainEvent;
