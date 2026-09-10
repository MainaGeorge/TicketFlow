using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Events.Models;

public abstract record EventBaseResult(Event? Event);
public sealed record EventCreatedResult(Event Event)
    : EventBaseResult(Event);
public sealed record EventNotFoundResult(Event? Event) : EventBaseResult(Event);
public sealed record EventResult(Event Event)
    : EventBaseResult(Event);
public sealed record PastEventResult(Event? Event)
    : EventBaseResult(Event);
