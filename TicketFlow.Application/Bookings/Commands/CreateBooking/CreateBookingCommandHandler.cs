using MediatR;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.ApplicationEvents.BookingCreated;
using TicketFlow.Application.Bookings.Exceptions;
using TicketFlow.Application.Bookings.Interfaces;
using TicketFlow.Application.Bookings.Models;
using TicketFlow.Application.Events.Interfaces;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Bookings.Commands.CreateBooking;

public sealed class CreateBookingCommandHandler(
    IBookingRepository bookingRepository,
    IEventsRepository eventsRepository,
    IPublisher publisher,
    ILogger<CreateBookingCommandHandler> logger) : IRequestHandler<CreateBookingCommand, BookingBaseResult>
{
    public async Task<BookingBaseResult> Handle(CreateBookingCommand command, CancellationToken cancellationToken)
    {
        var @event = await eventsRepository.GetEventAsync(command.EventId, cancellationToken);

        if(@event is null)
        {
            logger.LogWarning("User: {UserId} tried to book a non existent event: {EventId}", command.UserId, command.EventId);
            return new BookingEventNotFound();
        }

        var seat = await bookingRepository.GetSeatForBookingAsync(command.EventId, command.SeatId, cancellationToken);

        if (seat is null)
        {
            logger.LogWarning("Seat not found for event. EventId: {EventId}, SeatId: {SeatId}", command.EventId, command.SeatId);
            return new BookingSeatNotFound();
        }

        if (seat.Booking is not null)
        {
            logger.LogWarning("Seat is already booked. SeatId: {SeatId}, EventId: {EventId}", command.SeatId, command.EventId);
            return new BookingSeatAlreadyBooked();
        }

        if (seat.Event.EventDate < DateTime.UtcNow)
        {
            logger.LogWarning("Event is unavailable. SeatId: {SeatId}, EventId: {EventId}", command.SeatId, command.EventId);
            return new BookingEventUnavailable();
        }

        var booking = Booking.Create(command.UserId, command.SeatId, DateTime.UtcNow);

        await bookingRepository.AddAsync(booking, cancellationToken);

        try
        {
            await bookingRepository.SaveChangesAsync(command.SeatId, cancellationToken);

            await publisher.Publish(new BookingCreatedEvent(booking.Id, command.UserId), cancellationToken);

            return new BookingCreated(booking.Id, booking.SeatId, booking.UserId, booking.CreatedAt);
        }
        catch (SeatAlreadyBookedException)
        {
            logger.LogError("Seat booked by another user. SeatId: {SeatId}", command.SeatId);
            return new BookingSeatAlreadyBooked();
        }
    }
}
