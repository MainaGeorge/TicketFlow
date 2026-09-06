using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Presentation.Data;

namespace TicketFlow.Tests;

public class BookingsTests
{
    [Fact]
    public async Task CreateBooking_WithValidRequest_Returns201()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var (userToken, seatId) = await CreateUserAndSeat(client, factory);
        TestHelpers.SetBearerToken(client, userToken);
        var response = await client.PostAsJsonAsync("/api/bookings", new { seatId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }


    [Fact]
    public async Task CreateBooking_WithoutJwt_Returns401()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/bookings", new { seatId = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task CreateBooking_WithUnknownSeat_Returns404()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = await TestHelpers.RegisterAndLogin(client, "user@test.com");
        TestHelpers.SetBearerToken(client, token);
        var response = await client.PostAsJsonAsync("/api/bookings", new { seatId = 999999 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    [Fact]
    public async Task CreateBooking_WithAlreadyBookedSeat_Returns409()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var (user1Token, seatId) = await CreateUserAndSeat(client, factory, "user1@test.com");
        TestHelpers.SetBearerToken(client, user1Token);
        var firstResponse = await client.PostAsJsonAsync("/api/bookings", new { seatId });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var user2Token = await TestHelpers.RegisterAndLogin(client, "user2@test.com");
        TestHelpers.SetBearerToken(client, user2Token);
        var secondResponse = await client.PostAsJsonAsync("/api/bookings", new { seatId });
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }


    [Fact]
    public async Task GetMyBookings_ReturnsCurrentUsersBookings()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var (token, seatId) = await CreateUserAndSeat(client, factory);
        TestHelpers.SetBearerToken(client, token);

        var createResponse = await client.PostAsJsonAsync("/api/bookings", new { seatId });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var response = await client.GetAsync("/api/bookings/my");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bookings = await response.Content.ReadFromJsonAsync<List<BookingResponse>>();
        Assert.NotNull(bookings);
        Assert.Contains(bookings, booking => booking.Seat.Id == seatId);
    }


    [Fact]
    public async Task GetBooking_OtherUsersBooking_DoesNotReturnBooking()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        // User 1 creates a booking.
        var (user1Token, seatId) = await CreateUserAndSeat(client, factory, "user1@test.com");
        TestHelpers.SetBearerToken(client, user1Token);
        var createResponse = await client.PostAsJsonAsync("/api/bookings", new { seatId });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var booking = await createResponse.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(booking);

        // User 2 attempts to retrieve User 1's booking.
        var user2Token = await TestHelpers.RegisterAndLogin(client, "user2@test.com");
        TestHelpers.SetBearerToken(client, user2Token);
        var response = await client.GetAsync($"/api/bookings/{booking!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    [Fact]
    public async Task CreateBooking_WithoutSeatId_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = await TestHelpers.RegisterAndLogin(client, "user@test.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await client.PostAsJsonAsync("/api/bookings", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task CreateBooking_WithInvalidSeatId_Returns400(int seatId)
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = await TestHelpers.RegisterAndLogin(client, "user@test.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await client.PostAsJsonAsync("/api/bookings", new { seatId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Fact]
    public async Task CreateBooking_ConcurrentlyForSameSeat_OnlyOneSucceeds()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var setupClient = factory.CreateClient();

        // Create the seat first.
        var (_, seatId) = await CreateUserAndSeat(setupClient, factory, "setup@test.com");

        // Create two independent clients/users.
        var client1 = factory.CreateClient();
        var client2 = factory.CreateClient();
        var token1 = await TestHelpers.RegisterAndLogin(client1, "user1@test.com");
        var token2 = await TestHelpers.RegisterAndLogin(client2, "user2@test.com");
        TestHelpers.SetBearerToken(client1, token1);
        TestHelpers.SetBearerToken(client2, token2);

        // Start both requests at the same time.
        var task1 = client1.PostAsJsonAsync("/api/bookings", new { seatId });
        var task2 = client2.PostAsJsonAsync("/api/bookings", new { seatId });
        var responses = await Task.WhenAll(task1, task2);

        var successfulRequests = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictRequests = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, successfulRequests);
        Assert.Equal(1, conflictRequests);

        // Verify that the database contains exactly one booking.
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bookingCount = await dbContext.Bookings.CountAsync(b => b.SeatId == seatId);
        Assert.Equal(1, bookingCount);
    }

    private static async Task<(string Token, int SeatId)>
        CreateUserAndSeat(HttpClient client, CustomWebApplicationFactory factory, string email = "user@test.com")
    {
        var token = await TestHelpers.RegisterAndLogin(client, email);
        TestHelpers.SetBearerToken(client, token);

        var eventResponse = await client.PostAsJsonAsync("/api/events", new { name = "Test Concert", venue = "Test Arena", eventDate = DateTime.UtcNow.AddDays(30) });
        eventResponse.EnsureSuccessStatusCode();
        var eventDto = await eventResponse.Content.ReadFromJsonAsync<EventResponse>() ?? throw new InvalidOperationException("Event was not returned.");

        var seatResponse = await client.PostAsJsonAsync($"/api/events/{eventDto.Id}/seats", new { row = "A", number = 1, price = 50 });
        seatResponse.EnsureSuccessStatusCode();
        var seatDto = await seatResponse.Content.ReadFromJsonAsync<SeatResponse>();

        return seatDto == null ? throw new InvalidOperationException("Seat was not returned.") : ((string Token, int SeatId))(token, seatDto.Id);
    }


    private sealed class EventResponse
    {
        public int Id { get; set; }
    }


    private sealed class SeatResponse
    {
        public int Id { get; set; }
    }


    private sealed class BookingResponse
    {
        public int Id { get; set; }

        public SeatInfo Seat { get; set; } = new();
    }


    private sealed class SeatInfo
    {
        public int Id { get; set; }
    }
}