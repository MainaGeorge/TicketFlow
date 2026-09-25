using MediatR;
using TicketFlow.Application.Abstractions.Repositories;
using TicketFlow.Application.Bookings.Models;

namespace TicketFlow.Application.Bookings.Queries.GetBooking;

public class GetBookingQueryHandler(IBookingRepository bookingRepository) : IRequestHandler<GetBookingQuery, BookingBaseResult>
{
    public async Task<BookingBaseResult> Handle(GetBookingQuery command, CancellationToken cancellationToken = default)
    {
        var booking = await bookingRepository.GetBookingAsync(command.BookingId, command.UserId, cancellationToken);

        if (booking is null)
            return new BookingNotFound();

        return booking;
    }
}
