using Moq;
using TicketFlow.Application.Bookings.Interfaces;
using TicketFlow.Application.Bookings.Models;
using TicketFlow.Application.Bookings.Queries.GetUserBookings;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Tests.Application.Bookings.Queries.GetUserBooking;

public class GetUserBookingsQueryHandlerTests
{
    private readonly Mock<IBookingRepository> _repository;
    private readonly GetUserBookingsQueryHandler _handler;

    public GetUserBookingsQueryHandlerTests()
    {
        _repository = new Mock<IBookingRepository>();
        _handler = new GetUserBookingsQueryHandler(_repository.Object);
    }

    [Fact]
    public async Task Handle_WhenUserHasBookings_ReturnsBookings()
    {
        var createdAt = DateTime.UtcNow.AddDays(-10);
        var userId = Guid.NewGuid().ToString();
        var query = new GetUserBookingsQuery(userId);
        List<Booking> bookings = [new Booking { Id=1, SeatId=5, CreatedAt=createdAt}, new Booking { Id = 2, SeatId = 6, CreatedAt = createdAt }];

        _repository
            .Setup(x => x.GetUserBookingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookings);

        var handlerResults = await _handler.Handle(query, CancellationToken.None);

        var retrievedBooking = Assert.IsAssignableFrom<IEnumerable<BookingResult>>(handlerResults).ToList();

        Assert.Equal(2, retrievedBooking.Count);
        Assert.Contains(retrievedBooking, b => b.Booking!.Id == 1 && b.Booking!.CreatedAt == createdAt && b.Booking!.SeatId == 5);
        Assert.Contains(retrievedBooking, b => b.Booking!.Id == 2 && b.Booking!.CreatedAt == createdAt && b.Booking!.SeatId == 6);
        _repository.Verify(x => x.GetUserBookingsAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserHasNoBookings_ReturnsEmptyCollection()
    {
        List<Booking> bookings = [];

        var userId = Guid.NewGuid().ToString();
        var query = new GetUserBookingsQuery(userId);

        _repository
            .Setup(x => x.GetUserBookingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookings);

        var handlerResults = await _handler.Handle(query, CancellationToken.None);

        var retrievedBooking = Assert.IsType<IEnumerable<BookingResult>>(handlerResults, exactMatch: false).ToList();
        _repository.Verify(x => x.GetUserBookingsAsync(userId, It.IsAny<CancellationToken>()), Times.Once);

        Assert.Empty(retrievedBooking);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToRepository()
    {
        List<Booking> bookings = [];
        using var cancellationToken = new CancellationTokenSource();
        var token = cancellationToken.Token;

        var userId = Guid.NewGuid().ToString();
        var query = new GetUserBookingsQuery(userId);

        _repository
            .Setup(x => x.GetUserBookingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookings);

        var handlerResults = await _handler.Handle(query, token);

        _repository.Verify(x => x.GetUserBookingsAsync(userId, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_PropagatesException()
    {
        using var cancellationToken = new CancellationTokenSource();
        var token = cancellationToken.Token;
        var message = "something unexpected happened";
        var exception = new InvalidOperationException(message);

        var userId = Guid.NewGuid().ToString();
        var query = new GetUserBookingsQuery(userId);

        _repository
            .Setup(x => x.GetUserBookingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(query, token));

        Assert.Equal(message, error.Message);

        _repository.Verify(x => x.GetUserBookingsAsync(userId, token), Times.Once);
    }
}
