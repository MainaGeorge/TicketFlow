using Microsoft.Extensions.Logging;
using Moq;
using TicketFlow.Application.Bookings;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application;

public class BookingsServiceTests
{
    private readonly Mock<ILogger<BookingsService>> _logger;
    private readonly Mock<IBookingRepository> _repository;
    private readonly BookingsService _bookingService;
    private const string UserId = "userId";

    public BookingsServiceTests()
    {
        _logger = new Mock<ILogger<BookingsService>>();
        _repository = new Mock<IBookingRepository>();

        _bookingService = new BookingsService(_repository.Object, _logger.Object);
    }

    [Fact]
    public async Task GetBooking_WhenBookingExists_ReturnsBooking()
    {
        var bookingId = 1;

        _repository
            .Setup(x => x.GetBookingAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Booking { Id = 1, CreatedAt = DateTime.UtcNow, UserId = UserId });

        var booking = await _bookingService.GetBookingAsync(bookingId, UserId, CancellationToken.None);

        var result = Assert.IsType<BookingResult>(booking);

        _repository.Verify(x => x.GetBookingAsync(bookingId, UserId,It.IsAny<CancellationToken>()),Times.Once);

        Assert.Equal(bookingId, result.Booking!.Id);
    }

    [Fact]
    public async Task GetBooking_WhenBookingDoesNotExist_ReturnsNoBooking()
    {
        var bookingId = 1;

        _repository
            .Setup(x => x.GetBookingAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking?) null);

        var booking = await _bookingService.GetBookingAsync(bookingId, UserId, CancellationToken.None);
        _repository.Verify(x => x.GetBookingAsync(bookingId, UserId, It.IsAny<CancellationToken>()), Times.Once);

        var result = Assert.IsType<BookingNotFound>(booking);
    }

    [Fact]
    public async Task GetBookingAsync_PassesCancellationTokenToRepository()
    {
        var bookingId = 1;
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _repository
            .Setup(x => x.GetBookingAsync(bookingId, UserId,cancellationToken))
            .ReturnsAsync(new Booking
            {
                Id = bookingId,
                UserId = UserId
            });

        await _bookingService.GetBookingAsync(bookingId, UserId, cancellationToken);

        _repository.Verify(x => x.GetBookingAsync(bookingId, UserId, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GetBookingAsync_WhenCancellationRequested_PassesCancelledToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _repository
            .Setup(x => x.GetBookingAsync(It.IsAny<int>(), It.IsAny<string>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() => _bookingService.GetBookingAsync(1, UserId, cts.Token));
    }

    [Fact]
    public async Task GetBookingAsync_WhenRepositoryThrows_PropagatesException()
    {
        var exception = new InvalidOperationException("Unexpected failure");

        _repository
            .Setup(x => x.GetBookingAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _bookingService.GetBookingAsync(1, UserId, CancellationToken.None));

        Assert.Equal("Unexpected failure", result.Message);
    }

    [Fact]
    public async Task GetBookingsAsync_WhenBookingsExist_ReturnsBookings()
    {
        var bookingId = 1;
        List<Booking> bookings = [new Booking { Id = bookingId }, new Booking { Id = 2 }];

        _repository
            .Setup(x => x.GetUserBookingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookings);

        var result = await _bookingService.GetBookingsAsync(UserId, CancellationToken.None);

        var bookingResults = Assert.IsType<IEnumerable<BookingResult>>(result, exactMatch: false);
        Assert.Contains(bookingResults, booking => booking.Booking!.Id == bookingId);
        Assert.Equal(2, bookingResults.Count());
    }

    [Fact]
    public async Task GetBookingsAsync_WhenNoBookingsExist_ReturnsNoBookings()
    {
        _repository
            .Setup(x => x.GetUserBookingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _bookingService.GetBookingsAsync(UserId, CancellationToken.None);

        var bookingResults = Assert.IsType<IEnumerable<BookingResult>>(result, exactMatch: false);
        
        Assert.Empty(bookingResults);
    }

    [Fact]
    public async Task CreateBookingAsync_WhenValid_ReturnsCreated()
    {
        var seatId = 1;
        var seat = new Seat { Id = seatId, Event = new Event { EventDate = DateTime.UtcNow.AddDays(10) } };
        Booking? createdBooking = null;

        _repository
            .Setup(x => x.SaveChangesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repository
            .Setup(x => x.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Callback<Booking, CancellationToken>((booking, _) => createdBooking = booking)
            .Returns(Task.CompletedTask);

        _repository
            .Setup(x => x.GetSeatForBookingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        var result = await _bookingService.CreateBookingAsync(UserId, seatId, CancellationToken.None);

        _repository.Verify(x => x.AddAsync(It.Is<Booking>(b => b.UserId == UserId && b.SeatId == seatId), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(x => x.SaveChangesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(createdBooking);
        Assert.Equal(UserId, createdBooking.UserId);
        Assert.Equal(seatId, createdBooking.SeatId);

    }

    [Fact]
    public async Task CreateBookingAsync_PassesCancellationTokenToRepository()
    {
        var seatId = 1;
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var seat = new Seat
        {
            Id = seatId,
            Event = new Event { EventDate = DateTime.UtcNow.AddDays(10) }
        };

        _repository
            .Setup(x => x.GetSeatForBookingAsync(seatId, cancellationToken))
            .ReturnsAsync(seat);

        _repository
            .Setup(x => x.AddAsync(It.IsAny<Booking>(), cancellationToken))
            .Returns(Task.CompletedTask);

        _repository
            .Setup(x => x.SaveChangesAsync(It.IsAny<int>(), cancellationToken))
            .Returns(Task.CompletedTask);

        await _bookingService.CreateBookingAsync(UserId, seatId, cancellationToken);

        _repository.Verify(x => x.GetSeatForBookingAsync(seatId, cancellationToken), Times.Once);

        _repository.Verify(x => x.AddAsync(It.IsAny<Booking>(), cancellationToken), Times.Once);

        _repository.Verify(x => x.SaveChangesAsync(It.IsAny<int>(), cancellationToken), Times.Once);
    }

    [Fact]
    public async Task CreateBookingAsync_WhenRepositoryThrows_PropagatesException()
    {
        var seatId = 1;

        var seat = new Seat
        {
            Id = seatId,
            Event = new Event { EventDate = DateTime.UtcNow.AddDays(10) }
        };

        _repository
            .Setup(x => x.GetSeatForBookingAsync(seatId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        var exception = new InvalidOperationException("Database unavailable");

        _repository
            .Setup(x => x.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _bookingService.CreateBookingAsync(UserId, seatId,CancellationToken.None));

        Assert.Equal("Database unavailable", result.Message);
    }

    [Fact]
    public async Task CreateBookingAsync_WhenSeatNotFound_ReturnsSeatNotFound()
    {
        var seatId = 5;

        _repository
            .Setup(x => x.GetSeatForBookingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Seat?) null);

        var result = await _bookingService.CreateBookingAsync(UserId, seatId, CancellationToken.None);
        _repository.Verify(x => x.AddAsync(It.Is<Booking>(b => b.UserId == UserId && b.SeatId == seatId), It.IsAny<CancellationToken>()), Times.Never);

        Assert.IsType<BookingSeatNotFound>(result);
    }

    [Fact]
    public async Task CreateBookingAsync_WhenSeatAlreadyBooked_ReturnsAlreadyBooked()
    {
        var seat = new Seat { Id = 1, Booking = new Booking { Id = 2 }, Event = new Event { EventDate = DateTime.UtcNow.AddDays(10) } }; 

        _repository
            .Setup(x => x.GetSeatForBookingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        var result = await _bookingService.CreateBookingAsync(UserId, seat.Id, CancellationToken.None);

        _repository.Verify(x => x.AddAsync(It.Is<Booking>(b => b.UserId == UserId && b.SeatId == seat.Id), It.IsAny<CancellationToken>()), Times.Never);

        Assert.IsType<BookingSeatAlreadyBooked>(result);
    }

    [Fact]
    public async Task CreateBookingAsync_WhenEventUnavailable_ReturnsEventUnavailable()
    {
        var seat = new Seat { Id = 1, Event = new Event { EventDate = DateTime.UtcNow.AddDays(-10) } };

        _repository
            .Setup(x => x.GetSeatForBookingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

        var result = await _bookingService.CreateBookingAsync(UserId, seat.Id, CancellationToken.None);

        _repository.Verify(x => x.AddAsync(It.Is<Booking>(b => b.UserId == UserId && b.SeatId == seat.Id), It.IsAny<CancellationToken>()), Times.Never);

        Assert.IsType<BookingEventUnavailable>(result);
    }
}
