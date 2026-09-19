using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TicketFlow.Application.Bookings.Commands.CreateBooking;
using TicketFlow.Application.Bookings.Models;
using TicketFlow.Application.Bookings.Queries.GetBooking;
using TicketFlow.Application.Bookings.Queries.GetUserBookings;
using TicketFlow.Contracts.DTOs;
using TicketFlow.Domain.Entities;
using TicketFlow.Presentation.Controllers;

namespace TicketFlow.Tests.Presentation.Controllers;

public class BookingsControllerTests
{
    private readonly Mock<ILogger<BookingsController>> _logger;
    private readonly BookingsController _controller;
    private readonly Mock<ISender> _sender;

    public BookingsControllerTests()
    {
        _logger = new Mock<ILogger<BookingsController>>();
        _sender = new Mock<ISender>();

        _controller = new BookingsController(_sender.Object, _logger.Object);
        SetAuthenticatedUser("user-123");
    }

    private void SetAuthenticatedUser(string userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, "TestAuthentication");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
    }

    [Fact]
    public async Task CreateBooking_WhenServiceReturnsCreated_Returns201()
    {
        var bookingId = 10;

        _sender
            .Setup(x => x.Send(It.IsAny<CreateBookingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingCreated(bookingId, 5, "userId", DateTime.UtcNow));

        var request = new CreateBookingRequest { EventId = 1, SeatId = 5 };
        var result = await _controller.CreateBooking(request, CancellationToken.None);
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var bookingCreated = Assert.IsType<BookingDto>(createdResult.Value);

        Assert.Equal(nameof(BookingsController.GetBooking), createdResult.ActionName);
        Assert.Equal(bookingId, createdResult.RouteValues!["id"]);
    }

    [Fact]
    public async Task CreateBooking_WhenSeatNotFound_Returns404()
    {
        _sender
            .Setup(x => x.Send(It.IsAny<CreateBookingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingSeatNotFound());

        var request = new CreateBookingRequest { EventId = 1, SeatId = 5 };
        var result = await _controller.CreateBooking(request, CancellationToken.None);
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);

        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal("Seat not found.", problem.Title);
    }

    [Fact]
    public async Task CreateBooking_WhenSeatAlreadyBooked_Returns409()
    {
        _sender
            .Setup(x => x.Send(It.IsAny<CreateBookingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingSeatAlreadyBooked());

        var request = new CreateBookingRequest { EventId = 1, SeatId = 5 };
        var result = await _controller.CreateBooking(request, CancellationToken.None);
        var conflict = Assert.IsType<ConflictObjectResult>(result);

        var problem = Assert.IsType<ProblemDetails>(conflict.Value);

        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal("Seat is already booked.", problem.Title);
    }

    [Fact]
    public async Task CreateBooking_WhenEventUnavailable_Returns409()
    {
        _sender
            .Setup(x => x.Send(It.IsAny<CreateBookingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingEventUnavailable());

        var request = new CreateBookingRequest { EventId = 1, SeatId = 5 };
        var result = await _controller.CreateBooking(request, CancellationToken.None);
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);

        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal("Event has already started.", problem.Title);
    }

    [Fact]
    public async Task CreateBooking_WhenUserIsUnAuthenticated_Returns401()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } };
        var request = new CreateBookingRequest { EventId=1, SeatId = 5 };
        var result = await _controller.CreateBooking(request, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);

        _sender.Verify(x => x.Send(It.IsAny<CreateBookingCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetBooking_WhenBookingNotFound_Returns404()
    {
        _sender
            .Setup(x => x.Send(It.IsAny<GetBookingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingNotFound());

        var result = await _controller.GetBooking(10, CancellationToken.None);

        var notFound =Assert.IsType<NotFoundObjectResult>(result);

        var problem = Assert.IsType<ProblemDetails>(notFound.Value);

        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task GetBooking_WhenFound_Returns200()
    {
        var bookingResult = new BookingResult(10, 5, "user-123", DateTime.UtcNow, "paymentReference", "A", 1, 100m, 10);

        _sender
            .Setup(x => x.Send(It.IsAny<GetBookingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookingResult);

        var result = await _controller.GetBooking(10, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);

        var retrievedBooking = Assert.IsType<BookingDto>(okResult.Value);

        Assert.Equal(bookingResult.Id, retrievedBooking!.Id);
        Assert.Equal(bookingResult.SeatId, retrievedBooking.SeatId);
        Assert.Equal(bookingResult.UserId, retrievedBooking.UserId);
        Assert.Equal(bookingResult.SeatPrice, retrievedBooking.Price);
        Assert.Equal(bookingResult.SeatRow, retrievedBooking.SeatRow);
        Assert.Equal(bookingResult.SeatNumber, retrievedBooking.SeatNumber);
        Assert.Equal(bookingResult.EventId, retrievedBooking.EventId);
    }

    [Fact]
    public async Task GetMyBookings_WhenUserIsAuthenticated_ReturnsBookings()
    {
        _sender
            .Setup(x => x.Send(new GetUserBookingsQuery("user-123")))
            .ReturnsAsync([]);

        var result = await _controller.GetMyBookings(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetMyBookings_WhenUserIsNotAuthenticated_ReturnsUnauthorised()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } };

        _sender
            .Setup(x => x.Send(new GetUserBookingsQuery("user-123")))
            .ReturnsAsync([]);

        var result = await _controller.GetMyBookings(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        _sender.Verify(x => x.Send(It.IsAny<GetUserBookingsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}