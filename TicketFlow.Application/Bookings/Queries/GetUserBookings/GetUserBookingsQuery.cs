using MediatR;
using TicketFlow.Application.Bookings.Models;

namespace TicketFlow.Application.Bookings.Queries.GetUserBookings;

public sealed record GetUserBookingsQuery(string UserId) : IRequest<IEnumerable<BookingResult>>
{
}
