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
            return NotFound("Seat not found");

        if (seat.Booking is not null)
            return Conflict("Seat is already booked");

        var booking = new Models.Booking
        {
            SeatId = bookingRequest.SeatId,
            CreatedAt = DateTime.UtcNow,
            UserId = userId
        };

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var bookingDto = new BookingDto
        {
            Id = booking.Id,
            UserId = booking.UserId,
            SeatId = booking.SeatId,
            CreatedAt = booking.CreatedAt
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
