using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketFlow.Application.Events.Commands.CreateEvent;
using TicketFlow.Application.Events.Models;
using TicketFlow.Application.Events.Queries.GetAllEvents;
using TicketFlow.Application.Events.Queries.GetEvent;
using TicketFlow.Application.Seats;
using TicketFlow.Application.Seats.Commands;
using TicketFlow.Application.Seats.Queries.GetEventSeats;
using TicketFlow.Application.Seats.Queries.GetSeat;
using TicketFlow.Contracts.Events;
using TicketFlow.Contracts.Seats;
using TicketFlow.Presentation.Mappings;

namespace TicketFlow.Presentation.Controllers;

[Route("api/events")]
[ApiController]
[ApiVersion("1.0")]
[Authorize]
public class EventsController(ISender sender, ILogger<EventsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
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

        var @event = await sender.Send(new CreateEventCommand(request.Name!, request.Venue!, request.EventDate ?? DateTime.UtcNow, userId), cancellationToken);

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
                eventCreated.Event.MapToEventDto()),
            _ => throw new InvalidOperationException("Unknown booking result.")
        };
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEventById(int id, CancellationToken cancellationToken)
    {
        var eventQuery = new GetEventQuery(id);
        var @event = await sender.Send(eventQuery, cancellationToken);

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
            EventResult eventResult => Ok(eventResult.Event.MapToEventDto()),
            _ => throw new InvalidOperationException("Unknown booking result.")
        };
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllEvents(CancellationToken cancellationToken)
    {
        var query = new GetAllEventsQuery();
        var events = await sender.Send(query, cancellationToken);
        return Ok(events.Select(e => e.Event.MapToEventDto()));
    }

    [HttpPost("{eventId:int}/seats")]
    public async Task<IActionResult> CreateSeat(int eventId, [FromBody] CreateSeatRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
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

        var createSeatCommand = new CreateSeatCommand(eventId, request.Row, request.Number!.Value, request.Price!.Value);
        var createdSeat = await sender.Send(createSeatCommand, cancellationToken);

        return createdSeat switch
        {
            SeatCreatedResult result => CreatedAtAction(nameof(GetSeat), new { eventId = eventId, seatId = result.Seat!.Id }, result.Seat.MapToSeatDto()),
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
        var query = new GetSeatQuery(eventId, seatId);
        var seat = await sender.Send(query, cancellationToken);

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
            SeatResult result => Ok(result.Seat.MapToSeatDto()),
            _ => throw new InvalidOperationException("Unknown seat result.")
        };
    }

    [HttpGet("{eventId:int}/seats")]
    public async Task<IActionResult> GetSeats(int eventId, CancellationToken cancellationToken)
    {
        var query = new GetEventSeatsQuery(eventId);
        var seats = await sender.Send(query, cancellationToken);

        return Ok(seats.Select(s => s.Seat.MapToSeatDto()));
    }
}
