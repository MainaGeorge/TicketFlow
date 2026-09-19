namespace TicketFlow.Application.Bookings.Models;

public abstract record BookingBaseResult();
public sealed record BookingSeatNotFound : BookingBaseResult;
public sealed record BookingSeatAlreadyBooked : BookingBaseResult;
public sealed record BookingEventUnavailable : BookingBaseResult;
public sealed record BookingEventNotFound : BookingBaseResult;
public sealed record BookingNotFound : BookingBaseResult;
public record BookingResult
    (
        int Id,
        int SeatId,
        string UserId,
        DateTime CreatedAt,
        string PaymentReference,
        string SeatRow,
        int SeatNumber,
        decimal SeatPrice,
        int EventId
    )
    : BookingBaseResult;
public record BookingCreated
    (
        int Id,
        int SeatId,
        string UserId,
        DateTime CreatedAt
    )
    : BookingBaseResult;