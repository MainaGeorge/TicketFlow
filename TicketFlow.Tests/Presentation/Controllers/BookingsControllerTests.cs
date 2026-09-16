using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TicketFlow.Application.Bookings.Commands.CreateBooking;
using TicketFlow.Application.Bookings.Interfaces;
using TicketFlow.Application.Bookings.Models;
using TicketFlow.Application.Bookings.Queries.GetBooking;
using TicketFlow.Contracts.DTOs;
using TicketFlow.Domain.Entities;
using TicketFlow.Presentation.Controllers;

namespace TicketFlow.Tests.Presentation.Controllers;

public class BookingsControllerTests
{
    private readonly Mock<IBookingService> _bookingService;
    private readonly Mock<ILogger<BookingsController>> _logger;
    private readonly BookingsController _controller;
    private readonly Mock<ISender> _sender;

    public BookingsControllerTests()
    {
        _bookingService = new Mock<IBookingService>();
        _logger = new Mock<ILogger<BookingsController>>();
        _sender = new Mock<ISender>();

        _controller = new BookingsController(_sender.Object, _bookingService.Object, _logger.Object);
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

        var booking = new Booking
        {
            Id = 10,
            SeatId = 5,
            UserId = "user-123",
            CreatedAt = DateTime.UtcNow.AddDays(10),
            Seat = new Seat
            {
                Id = 5,
                EventId = 10,
                Row = "A",
                Number = 1,
                Price = 100
            }
        };

        _sender
            .Setup(x => x.Send(It.IsAny<CreateBookingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingCreated(booking));

        var request = new CreateBookingRequest { EventId = 1, SeatId = 5 };
        var result = await _controller.CreateBooking(request, CancellationToken.None);
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var bookingCreated = Assert.IsType<BookingDto>(createdResult.Value);

        Assert.Equal(nameof(BookingsController.GetBooking), createdResult.ActionName);
        Assert.Equal(10, createdResult.RouteValues!["id"]);
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

        _bookingService.Verify(x => x.CreateBookingAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
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
        var booking = new Booking
        {
            Id = 10,
            SeatId = 5,
            UserId = "user-123",
            CreatedAt = DateTime.UtcNow.AddDays(10),
            Seat = new Seat
            {
                Id = 5,
                EventId = 10,
                Row = "A",
                Number = 1,
                Price = 100
            }
        };

        _sender
            .Setup(x => x.Send(It.IsAny<GetBookingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingResult(booking));

        var result = await _controller.GetBooking(10, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);

        var retrievedBooking = Assert.IsType<BookingDto>(okResult.Value);

        Assert.Equal(booking.Id, retrievedBooking!.Id);
        Assert.Equal(booking.SeatId, retrievedBooking.SeatId);
        Assert.Equal(booking.UserId, retrievedBooking.UserId);
        Assert.Equal(booking.Seat.Price, retrievedBooking.Price);
        Assert.Equal(booking.Seat.Row, retrievedBooking.SeatRow);
        Assert.Equal(booking.Seat.Number, retrievedBooking.SeatNumber);
        Assert.Equal(booking.Seat.EventId, retrievedBooking.EventId);
    }

    [Fact]
    public async Task GetMyBookings_UsesAuthenticatedUserId()
    {
        _bookingService
            .Setup(x => x.GetBookingsAsync("user-123"))
            .ReturnsAsync([]);

        var result = await _controller.GetMyBookings();

        Assert.IsType<OkObjectResult>(result);

        _bookingService.Verify(x => x.GetBookingsAsync("user-123"), Times.Once);
    }
}