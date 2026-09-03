using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TicketFlow.Data;
using TicketFlow.DTOs;

namespace TicketFlow.Controllers;

[Route("api/bookings")]
[Authorize]
[ApiController]
public class BookingsController(AppDbContext context) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest bookingRequest)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId is null)
            return Unauthorized();

        var seat = await context
            .Seats
            .Include(s => s.Event)
            .Include(s => s.Booking)
            .FirstOrDefaultAsync(s => s.Id == bookingRequest.SeatId);

        if (seat is null)
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Seat not found.",
                Detail = $"Seat {bookingRequest.SeatId} not found.",
                Instance = HttpContext.Request.Path
            });

        if (seat.Booking is not null)
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Seat is already booked.",
                Detail = $"Seat {bookingRequest.SeatId} is already booked.",
                Instance = HttpContext.Request.Path
            });

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
        }
        catch (DbUpdateException)
        {
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
            return Unauthorized();

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
            return NotFound();

        return Ok(booking);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyBookings()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
            return Unauthorized();

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

        if (bookings is null)
            return NotFound();

        return Ok(bookings);
    }
}
