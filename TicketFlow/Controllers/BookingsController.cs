using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TicketFlow.Data;
using TicketFlow.DTOs;

namespace TicketFlow.Controllers;

[Route("api/bookings")]
[Authorize]
[ApiController]
public class BookingsController(AppDbContext context, ILogger<BookingsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest bookingRequest)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId is null)
        {
            logger.LogWarning("Booking request without authenticated user.");
            return Unauthorized();
        }

        var seat = await context
            .Seats
            .Include(s => s.Event)
            .Include(s => s.Booking)
            .FirstOrDefaultAsync(s => s.Id == bookingRequest.SeatId);

        if (seat is null)
        {
            logger.LogWarning("Seat not found. SeatId: {SeatId}", bookingRequest.SeatId);
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Seat not found.",
                Detail = $"Seat {bookingRequest.SeatId} not found.",
                Instance = HttpContext.Request.Path
            });
        }

        if (seat.Booking is not null)
        {
            logger.LogInformation("Booking rejected because seat is already booked. SeatId: {SeatId}, UserId: {UserId}", seat.Id, userId);
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Seat is already booked.",
                Detail = $"Seat {bookingRequest.SeatId} is already booked.",
                Instance = HttpContext.Request.Path
            });
        }

        if (seat.Event.EventDate <= DateTime.UtcNow)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Event has already started.",
                Detail = "Tickets cannot be booked for an event that has already started.",
                Instance = HttpContext.Request.Path
            });
        }

        var booking = new Models.Booking
        {
            SeatId = bookingRequest.SeatId!.Value,
            CreatedAt = DateTime.UtcNow,
            UserId = userId
        };

        context.Bookings.Add(booking);

        try
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Booking created successfully for user {UserId} and seat {SeatId}.", userId, bookingRequest.SeatId);
        }
        // we need to catch duplicate-key database error during this operation means the seat uniqueness constraint was hit."
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlException && sqlException.Number is 2601 or 2627)
        {
            logger.LogWarning("Concurrent booking attempt detected for SeatId: {SeatId}, UserId: {UserId}", bookingRequest.SeatId, userId);
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Seat is already booked.",
                Detail = $"Selected seat {bookingRequest.SeatId} was booked by another user.",
                Instance = HttpContext.Request.Path
            });
        }

        var bookingDto = new BookingDto
        {
            Id = booking.Id,
            UserId = booking.UserId,
            SeatId = booking.SeatId,
            CreatedAt = booking.CreatedAt,
            EventId = seat.Event.Id,
            SeatRow = seat.Row,
            SeatNumber = seat.Number,
            Price = seat.Price,
        };

        return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, bookingDto);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBooking(int id)
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

        var booking = await context
            .Bookings
            .Where(b => b.Id == id && b.UserId == userId)
            .Select(b => new
            {
                b.Id,
                b.CreatedAt,
                Seat = new
                {
                    b.Seat.Id,
                    b.Seat.Row,
                    b.Seat.Number,
                    b.Seat.Price
                },
                Event = new
                {
                    b.Seat.Event.Id,
                    b.Seat.Event.Name,
                    b.Seat.Event.EventDate
                }
            })
            .FirstOrDefaultAsync();

        if (booking == null)
        {
            logger.LogWarning("Booking not found. BookingId: {BookingId}", id);
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Booking not found.",
                Detail = $"The specified booking with id {id} could not be found.",
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(booking);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyBookings()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
        {
            logger.LogWarning("Booking request without authenticated user.");
            return Unauthorized();
        }

        var bookings = await context
            .Bookings
            .Where(b => b.UserId == userId)
            .Select(b => new
            {
                b.Id,
                b.CreatedAt,
                Seat = new
                {
                    b.Seat.Id,
                    b.Seat.Row,
                    b.Seat.Number,
                    b.Seat.Price
                },
                Event = new
                {
                    b.Seat.Event.Id,
                    b.Seat.Event.Name,
                    b.Seat.Event.EventDate
                }
            })
            .ToListAsync();

        return Ok(bookings);
    }
}
