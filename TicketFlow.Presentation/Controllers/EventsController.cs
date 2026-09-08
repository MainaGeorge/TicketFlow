using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketFlow.Application.Events;
using TicketFlow.Application.Seats;
using TicketFlow.Contracts.DTOs;
using TicketFlow.Presentation.Mappings;

namespace TicketFlow.Presentation.Controllers;

[Route("api/events")]
[ApiController]
[ApiVersion("1.0")]
[Authorize]
public class EventsController(IEventService eventService, ISeatsService seatsService, ILogger<EventsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request, CancellationToken cancellationToken)
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

        var newEvent = new Domain.Entities.Event
        {
            Name = request.Name!,
            EventDate = request.EventDate ?? DateTime.UtcNow,
            Venue = request.Venue!
        };

        var @event = await eventService.CreateEventAsync(newEvent, userId, cancellationToken);

        return @event switch
        {
            PastEventResult pastEventResult => BadRequest(
            new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid event date.",
                Detail = "Event date must be in the future.",
                Instance = HttpContext.Request.Path
            }),
            EventCreatedResult eventCreated => CreatedAtAction(
                nameof(GetEventById), new { id = eventCreated.Event!.Id },
                eventCreated.MapToEventDto()),
            _ => throw new InvalidOperationException("Unknown booking result.")
        };
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEventById(int id, CancellationToken cancellationToken)
    {
        var @event = await eventService.GetEventAsync(id, cancellationToken);

        return @event switch
        {
            EventNotFoundResult => NotFound(
            new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Event not found.",
                Detail = "The specified event could not be found.",
                Instance = HttpContext.Request.Path
            }),
            EventResult eventResult => Ok(eventResult.MapToEventDto()),
            _ => throw new InvalidOperationException("Unknown booking result.")
        };
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllEvents(CancellationToken cancellationToken)
    {
        var events = await eventService.GetAllEventsAsync(cancellationToken);
        return Ok(events.Select(e => e.MapToEventDto()));
    }

    [HttpPost("{eventId:int}/seats")]
    public async Task<IActionResult> CreateSeat(int eventId, [FromBody] CreateSeatRequest request, CancellationToken cancellationToken)
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

        var newSeat = new Domain.Entities.Seat
        {
            Row = request.Row,
            Number = request.Number!.Value,
            Price = request.Price!.Value,
            EventId = eventId
        };

        var createdSeat = await seatsService.CreateSeatAsync(eventId, newSeat, cancellationToken);

        return createdSeat switch
        {
            SeatCreatedResult result => CreatedAtAction(nameof(GetSeat), new { eventId = eventId, seatId = result.Seat!.Id }, result.MapToSeatDto()),
            EventNotFoundForSeatResult => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Event not found.",
                Detail = $"Event {eventId} not found.",
                Instance = HttpContext.Request.Path
            }),
            _ => throw new InvalidOperationException("Unknown seat result.")
        };
    }

    [HttpGet("{eventId:int}/seats/{seatId:int}")]
    public async Task<IActionResult> GetSeat(int eventId, int seatId, CancellationToken cancellationToken)
    {
        var seat = await seatsService.GetSeatAsync(eventId, seatId, cancellationToken);

        return seat switch
        {
            SeatNotFound => NotFound(
            new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Seat not found.",
                Detail = "The specified seat could not be found.",
                Instance = HttpContext.Request.Path
            }),
            SeatResult result => Ok(result.MapToSeatDto()),
            _ => throw new InvalidOperationException("Unknown seat result.")
        };
    }

    [HttpGet("{eventId:int}/seats")]
    public async Task<IActionResult> GetSeats(int eventId, CancellationToken cancellationToken)
    {
        var seats = await seatsService.GetSeatsAsync(eventId, cancellationToken);

        return Ok(seats.Select(s => s.MapToSeatDto()));
    }
}
