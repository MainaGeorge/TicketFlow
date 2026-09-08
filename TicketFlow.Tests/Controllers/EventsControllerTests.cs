using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TicketFlow.Application.Events;
using TicketFlow.Application.Seats;
using TicketFlow.Contracts.DTOs;
using TicketFlow.Domain.Entities;
using TicketFlow.Presentation.Controllers;

namespace TicketFlow.Tests.Controllers;

public class EventsControllerTests
{
    private readonly Mock<ISeatsService> _mockSeatsService;
    private readonly Mock<IEventsService> _mockEventsService;
    private readonly Mock<ILogger<EventsController>> _logger;
    private readonly EventsController _controller;
    private const string userId = "abc-123";

    private void SetAuthorisation(string userId)
    {
        var testIdentity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "TestAuthentication");

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(testIdentity) }
        };
    }

    public EventsControllerTests()
    {
        _mockEventsService = new Mock<IEventsService>();
        _mockSeatsService = new Mock<ISeatsService>();
        _logger = new Mock<ILogger<EventsController>>();

        _controller = new EventsController(_mockEventsService.Object, _mockSeatsService.Object, _logger.Object);

        SetAuthorisation(userId);
    }

    [Fact]
    public async Task CreatedEvent_WhenNotAuthenticated_Returns401()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } };

        var request = new CreateEventRequest
        {
            Name = "Rock Concert",
            Venue = "Arena",
            EventDate = DateTime.UtcNow.AddDays(30)
        };

        var result = await _controller.CreateEvent(request, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        _mockEventsService.Verify(x => x.CreateEventAsync(It.IsAny<Event>(), It.IsAny<string>()), Times.Never());
    }

    [Fact]
    public async Task CreatedEvent_WhenCreated_Returns201()
    {
        _mockEventsService
            .Setup(x => x.CreateEventAsync(It.IsAny<Event>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventCreatedResult(new Event() { Id = 10 }));

        var request = new CreateEventRequest
        {
            Name = "Rock Concert",
            Venue = "Arena",
            EventDate = DateTime.UtcNow.AddDays(30)
        };

        var result = await _controller.CreateEvent(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);

        Assert.Equal(nameof(EventsController.GetEventById), created.ActionName);
        Assert.Equal(10, created.RouteValues!["id"]);
    }

    [Fact]
    public async Task CreatedEvent_WhenEventIsInPast_Returns400()
    {
        _mockEventsService
            .Setup(x => x.CreateEventAsync(It.IsAny<Event>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PastEventResult(null));

        var request = new CreateEventRequest
        {
            Name = "Test Event",
            Venue = "Arena",
            EventDate = DateTime.UtcNow.AddDays(-3)
        };

        var result = await _controller.CreateEvent(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Equal("Invalid event date.", problem.Title);
    }

    [Fact]
    public async Task GetEventById_WhenNotFound_Returns400()
    {
        _mockEventsService
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventNotFoundResult(null));

        var result = await _controller.GetEventById(10, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);

        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        Assert.Equal("Event not found.", problem.Title);
    }

    [Fact]
    public async Task GetEventById_WhenEventExists_Returns200()
    {
        _mockEventsService
            .Setup(x => x.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventResult(new Event()));

        var result = await _controller.GetEventById(10, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task GetAllEvents_AlwaysReturns200()
    {
        _mockEventsService
            .Setup(x => x.GetAllEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _controller.GetAllEvents(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task CreateTask_WhenNotAuthenticated_Returns401()
    {
        _controller.ControllerContext = new ControllerContext {  HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }  };

        var request = new CreateSeatRequest { Row = "A", Number = 1, Price = 200 };

        var result = await _controller.CreateSeat(1, request, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        _mockSeatsService.Verify(x => x.CreateSeatAsync(It.IsAny<int>(), It.IsAny<Seat>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task CreateSeat_WhenEventExists_Returns201()
    {
        var seatId = 100;
        var eventId = 4;

        _mockSeatsService
            .Setup(x => x.CreateSeatAsync(It.IsAny<int>(), It.IsAny<Seat>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeatCreatedResult(new Seat { Id = seatId }));

        var request = new CreateSeatRequest { Row = "A", Number = 1, Price = 200 };

        var result = await _controller.CreateSeat(eventId, request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(EventsController.GetSeat), created.ActionName);
        Assert.Equal(4, created.RouteValues!["eventId"]);
        Assert.Equal(100, created.RouteValues!["seatId"]);
    }

    [Fact]
    public async Task CratedSeat_WhenEventNotExists_Returns404()
    {
        _mockSeatsService
            .Setup(x => x.CreateSeatAsync(It.IsAny<int>(), It.IsAny<Seat>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventNotFoundForSeatResult(null));

        var request = new CreateSeatRequest { Row = "A", Number = 1, Price = 200 };

        var result = await _controller.CreateSeat(1, request, CancellationToken.None);
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFoundResult.Value);

        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        Assert.Equal("Event not found.", problem.Title);
    }

    [Fact]
    public async Task GetSeats_AlwaysReturns200()
    {
        _mockSeatsService
            .Setup(x => x.GetSeatsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _controller.GetSeats(4, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task GetSeat_WhenSeatNotFound_Returns404()
    {
        _mockSeatsService
            .Setup(x => x.GetSeatAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeatNotFound(null));

        var result = await _controller.GetSeat(1, 4, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFoundResult.Value);

        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
        Assert.Equal("Seat not found.", problem.Title);
    }

    [Fact]
    public async Task GetSeat_WhenSeatExists_Returns200()
    {
        var seatId = 4;

        _mockSeatsService
            .Setup(x => x.GetSeatAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SeatResult(new Seat { Id = seatId }));

        var result = await _controller.GetSeat(1, 4, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }
}
