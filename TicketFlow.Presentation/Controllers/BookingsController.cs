using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketFlow.Application.Bookings;
using TicketFlow.Contracts.DTOs;
using TicketFlow.Presentation.Mappings;

namespace TicketFlow.Presentation.Controllers;

[Route("api/bookings")]
[Authorize]
[ApiVersion("1.0")]
[ApiController]
public class BookingsController(IBookingService bookingService, ILogger<BookingsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest bookingRequest, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId is null)
        {
            logger.LogWarning("Booking request without authenticated user.");
            return Unauthorized();
        }

        if (userId is null)
            return Unauthorized();

        var result = await bookingService.CreateBookingAsync(userId, bookingRequest.SeatId!.Value, cancellationToken);


        return result switch
        {
            BookingCreated created =>
                CreatedAtAction(
                    nameof(GetBooking),
                    new { id = created.Booking!.Id },
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

        if (userId == null)
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

        var booking = await bookingService.GetBookingAsync(id, userId, cancellationToken);
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
    public async Task<IActionResult> GetMyBookings()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
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

        var bookings = await bookingService.GetBookingsAsync(userId);
        return Ok(bookings.Select(b => b.MapToBookingDto(userId)));
    }
}
