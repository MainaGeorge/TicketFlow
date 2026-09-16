using MediatR;
using TicketFlow.Application.Bookings.Interfaces;
using TicketFlow.Application.Bookings.Models;

namespace TicketFlow.Application.Bookings.Queries.GetUserBookings;

public class GetUserBookingsQueryHandler(IBookingRepository bookingRepository) : IRequestHandler<GetUserBookingsQuery, IEnumerable<BookingResult>>
{
    public async Task<IEnumerable<BookingResult>> Handle(GetUserBookingsQuery request, CancellationToken cancellationToken)
    {
        var bookings =  await bookingRepository.GetUserBookingsAsync(request.UserId, cancellationToken);

        return bookings.Select(x => new BookingResult(x));
    }
}
