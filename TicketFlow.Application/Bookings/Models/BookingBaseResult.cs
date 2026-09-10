using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Bookings.Models;

public abstract record BookingBaseResult(Booking? Booking = null);
public sealed record BookingCreated(Booking Booking) : BookingBaseResult(Booking);
public sealed record BookingSeatNotFound : BookingBaseResult;
public sealed record BookingSeatAlreadyBooked : BookingBaseResult;
public sealed record BookingEventUnavailable : BookingBaseResult;
public sealed record BookingNotFound : BookingBaseResult;
public sealed record BookingResult(Booking Booking) : BookingBaseResult(Booking);

