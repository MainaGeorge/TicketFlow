using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Seats.Models;

public abstract record SeatBaseResult;
public sealed record SeatsCreatedResult(IEnumerable<Seat> Seats) : SeatBaseResult;
public sealed record SeatResult(Seat Seat) : SeatBaseResult;
public sealed record SeatNotFound : SeatBaseResult;
public sealed record EventNotFoundForSeatResult : SeatBaseResult;
public sealed record DuplicateSeatResult : SeatBaseResult;
