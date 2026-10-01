namespace TicketFlow.Contracts.IntegrationEvents;

public sealed record BookingCreatedIntegrationEvent(int BookingId, string UserId);
