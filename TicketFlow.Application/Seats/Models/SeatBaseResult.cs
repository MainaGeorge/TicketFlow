using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Seats;

public abstract record SeatBaseResult();
public sealed record SeatCreatedResult(Seat Seat) : SeatBaseResult();
public sealed record SeatResult(Seat Seat) : SeatBaseResult();
public sealed record SeatNotFound() : SeatBaseResult();
public sealed record EventNotFoundForSeatResult() : SeatBaseResult();
