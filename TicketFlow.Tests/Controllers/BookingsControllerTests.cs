using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TicketFlow.Application.Bookings;
using TicketFlow.Contracts.DTOs;
using TicketFlow.Domain.Entities;
using TicketFlow.Presentation.Controllers;

namespace TicketFlow.Tests.Controllers;

public class BookingsControllerTests
{
    private readonly Mock<IBookingService> _bookingService;
    private readonly Mock<ILogger<BookingsController>> _logger;
    private readonly BookingsController _controller;

    public BookingsControllerTests()
    {
        _bookingService = new Mock<IBookingService>();
        _logger = new Mock<ILogger<BookingsController>>();
        _controller = new BookingsController(_bookingService.Object, _logger.Object);
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

        _bookingService
            .Setup(x => x.CreateBookingAsync("user-123", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingCreated(booking));

        var request = new CreateBookingRequest { SeatId = 5 };
        var result = await _controller.CreateBooking(request, CancellationToken.None);
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);

        Assert.Equal(nameof(BookingsController.GetBooking), createdResult.ActionName);
        Assert.Equal(10, createdResult.RouteValues!["id"]);
    }

    [Fact]
    public async Task CreateBooking_WhenSeatNotFound_Returns404()
    {
        _bookingService
            .Setup(x => x.CreateBookingAsync("user-123", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingSeatNotFound());

        var request = new CreateBookingRequest { SeatId = 5 };
        var result = await _controller.CreateBooking(request, CancellationToken.None);
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);

        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal("Seat not found.", problem.Title);
    }

    [Fact]
    public async Task CreateBooking_WhenSeatAlreadyBooked_Returns409()
    {
        _bookingService
            .Setup(x => x.CreateBookingAsync("user-123", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingSeatAlreadyBooked());

        var request = new CreateBookingRequest { SeatId = 5 };
        var result = await _controller.CreateBooking(request, CancellationToken.None);
        var conflict = Assert.IsType<ConflictObjectResult>(result);

        var problem = Assert.IsType<ProblemDetails>(conflict.Value);

        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal("Seat is already booked.", problem.Title);
    }

    [Fact]
    public async Task CreateBooking_WhenEventUnavailable_Returns409()
    {
        _bookingService
            .Setup(x => x.CreateBookingAsync("user-123", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingEventUnavailable());

        var request = new CreateBookingRequest { SeatId = 5 };
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
        var request = new CreateBookingRequest { SeatId = 5 };
        var result = await _controller.CreateBooking(request, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);

        _bookingService.Verify(x => x.CreateBookingAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetBooking_WhenBookingNotFound_Returns404()
    {
        _bookingService
            .Setup(x => x.GetBookingAsync(10, "user-123", It.IsAny<CancellationToken>()))
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

        _bookingService
            .Setup(x => x.GetBookingAsync(10, "user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BookingResult(booking));

        var result = await _controller.GetBooking(10, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
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