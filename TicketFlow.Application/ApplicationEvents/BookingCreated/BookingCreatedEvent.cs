using MediatR;

namespace TicketFlow.Application.ApplicationEvents.BookingCreated;

public sealed record BookingCreatedEvent(int BookingId, string UserId) : INotification;
