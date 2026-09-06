using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TicketFlow.Presentation.Data;
using TicketFlow.Presentation.DTOs;

namespace TicketFlow.Presentation.Controllers;

[Route("api/events")]
[ApiController]
[ApiVersion("1.0")]
[Authorize]
public class EventsController(AppDbContext context, ILogger<EventsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
        {
            logger.LogWarning("Event creation request without authenticated user.");

            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized.",
                Detail = "You must be logged in to create events.",
                Instance = HttpContext.Request.Path
            });
        }

        if (!request.EventDate.HasValue || request.EventDate <= DateTime.UtcNow)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid event date.",
                Detail = "Event date must be in the future.",
                Instance = HttpContext.Request.Path
            });
        }

        var newEvent = new Models.Event
        {
            Name = request.Name!,
            EventDate = request.EventDate.Value,
            Venue = request.Venue!
        };
        context.Events.Add(newEvent);
        await context.SaveChangesAsync();

        logger.LogInformation("Event created: {EventId}, Name: {EventName}, Venue: {EventVenue}, Date: {EventDate}", newEvent.Id, newEvent.Name, newEvent.Venue, newEvent.EventDate);

        return CreatedAtAction(nameof(GetEventById), new { id = newEvent.Id }, new EventDto { Id = newEvent.Id, Name = newEvent.Name, Venue = newEvent.Venue, EventDate = newEvent.EventDate });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetEventById(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
        {
            logger.LogWarning("Event view request without an authenticated user. EventId: {EventId}", id);

            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized.",
                Detail = "You must be logged in to view events.",
                Instance = HttpContext.Request.Path
            });
        }

        var @event = await context.Events
            .Where(e => e.Id == id)
            .Select(e => new EventDto
            {
                Id = e.Id,
                Name = e.Name,
                Venue = e.Venue,
                EventDate = e.EventDate,
                TotalSeats = e.Seats.Count,
                AvailableSeats = e.Seats.Count(s => s.Booking == null)
            })
            .FirstOrDefaultAsync();

        if (@event == null)
        {
            logger.LogWarning("Event not found. EventId: {EventId}", id);
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Event not found.",
                Detail = "The specified event could not be found.",
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(@event);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllEvents()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
        {
            logger.LogWarning("Event view request without an authenticated user.");

            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized.",
                Detail = "You must be logged in to view events.",
                Instance = HttpContext.Request.Path
            });
        }
        var events = await context
            .Events
            .Include(e => e.Seats)
            .Select(e => new EventDto
            {
                Id = e.Id,
                Name = e.Name,
                Venue = e.Venue,
                EventDate = e.EventDate,
                TotalSeats = e.Seats.Count,
                AvailableSeats = e.Seats.Count(s => s.Booking == null)
            })
            .ToListAsync();

        return Ok(events);
    }

    [HttpPost("{eventId:int}/seats")]
    public async Task<IActionResult> CreateSeat(int eventId, [FromBody] CreateSeatRequest request)
    {
        var @event = await context.Events.FindAsync(eventId);
        if (@event == null)
        {
            logger.LogWarning("Event not found. EventId: {EventId}", eventId);
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Event not found.",
                Detail = $"Event {eventId} not found.",
                Instance = HttpContext.Request.Path
            });
        }

        var newSeat = new Models.Seat
        {
            Row = request.Row,
            Number = request.Number!.Value,
            Price = request.Price!.Value,
            EventId = eventId
        };
        context.Seats.Add(newSeat);
        await context.SaveChangesAsync();

        logger.LogInformation("Seat created Id: {SeatId}, Row: {SeatRow}, Number: {SeatNumber}, Price: {SeatPrice}, EventId: {EventId}", newSeat.Id, newSeat.Row, newSeat.Number, newSeat.Price, newSeat.EventId);

        var seatDto = new SeatDto { Id = newSeat.Id, Row = newSeat.Row, Number = newSeat.Number, Price = newSeat.Price, EventId = newSeat.EventId };

        return CreatedAtAction(nameof(GetSeat), new { eventId = @event.Id, seatId = newSeat.Id }, seatDto);
    }

    [HttpGet("{eventId:int}/seats/{seatId:int}")]
    public async Task<IActionResult> GetSeat(int eventId, int seatId)
    {
        var seat = await context
            .Seats
            .Where(s => s.Id == seatId && s.EventId == eventId)
            .Select(s => new SeatDto
            {
                Id = s.Id,
                Row = s.Row,
                Number = s.Number,
                Price = s.Price,
                EventId = s.EventId,
                IsBooked = s.Booking != null,
                BookingId = s.Booking != null ? s.Booking.Id : null
            })
            .FirstOrDefaultAsync();

        if (seat == null)
        {
            logger.LogWarning("Seat not found. EventId: {EventId}, SeatId: {SeatId}", eventId, seatId);
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Seat not found.",
                Detail = "The specified seat could not be found.",
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(seat);
    }

    [HttpGet("{eventId:int}/seats")]
    public async Task<IActionResult> GetSeats(int eventId)
    {
        var seat = await context
            .Seats
            .Where(s => s.EventId == eventId)
            .Select(s => new SeatDto
            {
                Id = s.Id,
                Row = s.Row,
                Number = s.Number,
                Price = s.Price,
                EventId = s.EventId,
                IsBooked = s.Booking != null,
                BookingId = s.Booking != null ? s.Booking.Id : null
            })
            .ToListAsync();

        return Ok(seat);
    }
}
