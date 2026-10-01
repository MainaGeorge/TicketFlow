using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Events.Models;

public abstract record EventBaseResult();
public sealed record EventCreatedResult(Event Event) : EventBaseResult();
public sealed record EventNotFoundResult() : EventBaseResult();
public sealed record EventResult(Event Event) : EventBaseResult();
public sealed record PastEventResult(Event Event) : EventBaseResult();
