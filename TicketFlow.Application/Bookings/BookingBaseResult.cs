using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Bookings;

public abstract record BookingBaseResult(string? UserId=null, int? Id=null, DateTime? CreatedAt=null, Seat? Seat=null);
public sealed record BookingCreated(string? UserId=null, int? Id=null, DateTime? CreatedAt=null, Seat? Seat=null) : BookingBaseResult(UserId, Id, CreatedAt, Seat);
public record BookingSeatNotFound : BookingBaseResult;
public record BookingSeatAlreadyBooked : BookingBaseResult;
public record BookingEventUnavailable: BookingBaseResult;
public record BookingResult(string? UserId = null, int? Id = null, DateTime? CreatedAt = null, Seat? Seat = null) : BookingBaseResult(UserId, Id, CreatedAt, Seat);

