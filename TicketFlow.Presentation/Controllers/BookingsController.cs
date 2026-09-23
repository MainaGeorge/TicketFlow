using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketFlow.Application.Bookings.Commands.CreateBooking;
using TicketFlow.Application.Bookings.Models;
using TicketFlow.Application.Bookings.Queries.GetBooking;
using TicketFlow.Application.Bookings.Queries.GetUserBookings;
using TicketFlow.Contracts.Booking;
using TicketFlow.Presentation.Mappings;

namespace TicketFlow.Presentation.Controllers;

[Route("api/bookings")]
[Authorize]
[ApiVersion("1.0")]
[ApiController]
public class TestController(ISender sender, ILogger<TestController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest bookingRequest, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogWarning("Booking request without authenticated user.");
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized.",
                Detail = "You must be logged in to create a booking.",
                Instance = HttpContext.Request.Path
            });
        }

        var createBookingCommand = new CreateBookingCommand(bookingRequest.EventId, bookingRequest.SeatId, userId);

        var result = await sender.Send(createBookingCommand, cancellationToken);


        return result switch
        {
            BookingCreated created =>
                CreatedAtAction(
                    nameof(GetBooking),
                    new { id = created.Id },
                    created.MapToBookingDto(userId)),

            BookingSeatNotFound =>
                NotFound(
                new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Seat not found.",
                    Detail = $"Seat {bookingRequest.SeatId} not found.",
                    Instance = HttpContext.Request.Path
                }),

            BookingEventNotFound =>
                NotFound(
                new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Booking event not found.",
                    Detail = $"Event {bookingRequest.EventId} not found.",
                    Instance = HttpContext.Request.Path
                }),

            BookingSeatAlreadyBooked =>
                Conflict(
                new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Seat is already booked.",
                    Detail = $"Seat {bookingRequest.SeatId} is already booked.",
                    Instance = HttpContext.Request.Path
                }),

            BookingEventUnavailable =>
                Conflict(
                new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Event has already started.",
                    Detail = "Tickets cannot be booked for an event that has already started.",
                    Instance = HttpContext.Request.Path
                }),

            _ => throw new InvalidOperationException("Unknown booking result.")
        };
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetBooking(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogWarning("Booking request without authenticated user.");

            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized.",
                Detail = "You must be logged in to view a booking.",
                Instance = HttpContext.Request.Path
            });
        }

        var bookingQuery = new GetBookingQuery(id, userId);

        var booking = await sender.Send(bookingQuery, cancellationToken);
        return booking switch
        {
            BookingNotFound => NotFound(
            new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Booking not found.",
                Detail = $"The specified booking with id {id} could not be found.",
                Instance = HttpContext.Request.Path
            }),
            BookingResult bookingResult => Ok(bookingResult.MapToBookingDto(userId)),
            _ => throw new InvalidOperationException("Unknown booking result.")
        };
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyBookings(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogWarning("Booking request without authenticated user.");
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized.",
                Detail = "You must be logged in to view your bookings.",
                Instance = HttpContext.Request.Path
            });
        }

        var bookings = await sender.Send(new GetUserBookingsQuery(userId), cancellationToken);
        return Ok(bookings.Select(b => b.MapToBookingDto(userId)));
    }
}
