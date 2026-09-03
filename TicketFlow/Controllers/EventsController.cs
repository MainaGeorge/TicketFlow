using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Data;
using TicketFlow.DTOs;

namespace TicketFlow.Controllers;

[Route("api/events")]
[ApiController]
[Authorize]
public class EventsController(AppDbContext context) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
    {
        var newEvent = new Models.Event
        {
            Name = request.Name,
            EventDate = request.EventDate,
            Venue = request.Venue
        };
        context.Events.Add(newEvent);
        await context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetEventById), new { id = newEvent.Id }, new EventDto { Id = newEvent.Id, Name = newEvent.Name, Venue = newEvent.Venue, EventDate = newEvent.EventDate });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetEventById(int id)
    {
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
            return NotFound();

        return Ok(@event);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllEvents()
    {
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

    [HttpPost("{eventId}/seats")]
    public async Task<IActionResult> CreateSeat(int eventId, [FromBody] CreateSeatRequest request)
    {
        var @event = await context.Events.FindAsync(eventId);
        if (@event == null)
            return NotFound();

        var newSeat = new Models.Seat
        {
            Row = request.Row,
            Number = request.Number,
            Price = request.Price,
            EventId = eventId
        };
        context.Seats.Add(newSeat);
        await context.SaveChangesAsync();

        var seatDto = new SeatDto { Id = newSeat.Id, Row = newSeat.Row, Number = newSeat.Number, Price = newSeat.Price, EventId = newSeat.EventId };

        return CreatedAtAction(nameof(GetEventById), new { id = @event.Id }, seatDto);
    }

    [HttpGet("{eventId}/seats/{seatId}")]
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
            return NotFound();

        return Ok(seat);
    }

    [HttpGet("{eventId}/seats")]
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

        if (seat == null)
            return NotFound();

        return Ok(seat);
    }
}
