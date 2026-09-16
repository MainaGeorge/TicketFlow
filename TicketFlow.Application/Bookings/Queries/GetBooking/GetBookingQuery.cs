using MediatR;
using TicketFlow.Application.Bookings.Models;

namespace TicketFlow.Application.Bookings.Queries.GetBooking;

public sealed record GetBookingQuery(int BookingId, string UserId) : IRequest<BookingBaseResult>;
