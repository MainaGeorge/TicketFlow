using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Seats;

public abstract record SeatBaseResult(Seat? Seat);
public sealed record SeatCreatedResult(Seat Seat) : SeatBaseResult(Seat);
public sealed record SeatResult(Seat Seat) : SeatBaseResult(Seat);
public sealed record SeatNotFound(Seat? Seat = null) : SeatBaseResult(Seat);
public sealed record EventNotFoundForSeatResult(Seat? Seat = null) : SeatBaseResult(Seat);
