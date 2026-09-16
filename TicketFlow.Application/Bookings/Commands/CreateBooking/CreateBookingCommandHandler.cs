using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TicketFlow.Application.Background;
using TicketFlow.Application.Bookings.Exceptions;
using TicketFlow.Application.Bookings.Interfaces;
using TicketFlow.Application.Bookings.Models;
using TicketFlow.Application.Events.Interfaces;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Bookings.Commands.CreateBooking;

public sealed class CreateBookingCommandHandler(
    IBookingRepository bookingRepository,
    IEventsRepository eventsRepository,
    IBackgroundTaskQueue backgroundTaskQueue,
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

        var booking = new Booking
        {
            UserId = command.UserId,
            SeatId = command.SeatId,
            CreatedAt = DateTime.UtcNow
        };

        await bookingRepository.AddAsync(booking, cancellationToken);

        try
        {
            await bookingRepository.SaveChangesAsync(command.SeatId, cancellationToken);

            var confirmationWork = new BookingConfirmationWork(booking.Id, booking.UserId);

            var payload = JsonSerializer.Serialize(confirmationWork);

            await backgroundTaskQueue.QueueAsync(
                new BackgroundWorkItem(
                    JobType: nameof(BookingConfirmationWork),
                    Payload: payload,
                    ExecuteAsync: async (serviceProvider, ct) =>
                    {
                        var processor = serviceProvider.GetRequiredService<IBookingConfirmationProcessor>();
                        await processor.ProcessAsync(confirmationWork, ct);
                    }),
                cancellationToken);

            return new BookingCreated(booking);
        }
        catch (SeatAlreadyBookedException)
        {
            logger.LogError("Seat booked by another user. SeatId: {SeatId}", command.SeatId);
            return new BookingSeatAlreadyBooked();
        }
    }
}
