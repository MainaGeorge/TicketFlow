using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Tests.Integration;

public class BookingEndpointTests
{
    [Fact]
    public async Task CreateBooking_WithValidRequest_Returns201()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var (userToken, eventId, seatId) = await CreateUserAndSeat(factory, client);
        TestHelpers.SetBearerToken(client, userToken);
        var response = await client.PostAsJsonAsync("/api/bookings", new { eventId, seatId });
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
        var token = await TestHelpers.RegisterAndLoginAsAdmin(factory, client, "user@test.com");
        TestHelpers.SetBearerToken(client, token);
        var eventResponse = await client.PostAsJsonAsync("api/events", new { name = "Test Concert", venue = "Test Arena", eventDate = DateTime.UtcNow.AddDays(30) });
        var eventDto = await eventResponse.Content.ReadFromJsonAsync<EventResponse>();
        var response = await client.PostAsJsonAsync("/api/bookings", new { eventId = eventDto!.Id, seatId = 999999 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_WithSeatFromAnotherEvent_Returns404()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = await TestHelpers.RegisterAndLoginAsAdmin(factory, client, "user@test.com");
        TestHelpers.SetBearerToken(client, token);
        var eventResponse1 = await client.PostAsJsonAsync("api/events", new { name = "Test Concert", venue = "Test Arena", eventDate = DateTime.UtcNow.AddDays(30) });
        var eventResponse2 = await client.PostAsJsonAsync("api/events", new { name = "Test Sports Concert", venue = "Test Arena", eventDate = DateTime.UtcNow.AddDays(30) });
        var eventDto1 = await eventResponse1.Content.ReadFromJsonAsync<EventResponse>();
        var eventDto2 = await eventResponse2.Content.ReadFromJsonAsync<EventResponse>();
        var payload = new
        {
            seats = new[]
            {
                new
                {
                    row = "A",
                    number = 1,
                    price = 50m
                }
            }
        };
        var seatResponse = await client.PostAsJsonAsync($"/api/events/{eventDto1!.Id}/seats", payload);
        seatResponse.EnsureSuccessStatusCode();
        var seatsDto = await seatResponse.Content.ReadFromJsonAsync<List<SeatDto>>();
        var response = await client.PostAsJsonAsync("/api/bookings", new { eventId = eventDto2!.Id, seatId = seatsDto!.First().Id });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_WithUnknownEvent_Returns404()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = await TestHelpers.RegisterAndLogin(client, "user@test.com");
        TestHelpers.SetBearerToken(client, token);
        var response = await client.PostAsJsonAsync("/api/bookings", new { eventId = 41250, seatId = 999999 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    [Fact]
    public async Task CreateBooking_WithAlreadyBookedSeat_Returns409()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var (user1Token, eventId, seatId) = await CreateUserAndSeat(factory, client, "user1@test.com");
        TestHelpers.SetBearerToken(client, user1Token);
        var firstResponse = await client.PostAsJsonAsync("/api/bookings", new { eventId, seatId });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var user2Token = await TestHelpers.RegisterAndLogin(client, "user2@test.com");
        TestHelpers.SetBearerToken(client, user2Token);
        var secondResponse = await client.PostAsJsonAsync("/api/bookings", new { eventId, seatId });
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }


    [Fact]
    public async Task GetMyBookings_ReturnsCurrentUsersBookings()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var (token, eventId, seatId) = await CreateUserAndSeat(factory, client);
        TestHelpers.SetBearerToken(client, token);

        var createResponse = await client.PostAsJsonAsync("/api/bookings", new { eventId, seatId });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var response = await client.GetAsync("/api/bookings/my");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bookings = await response.Content.ReadFromJsonAsync<List<BookingResponse>>();
        Assert.NotNull(bookings);
        Assert.Contains(bookings, booking => booking.SeatId == seatId);
    }


    [Fact]
    public async Task GetBooking_OtherUsersBooking_DoesNotReturnBooking()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        // User 1 creates a booking.
        var (user1Token, eventId, seatId) = await CreateUserAndSeat(factory, client, "user1@test.com");
        TestHelpers.SetBearerToken(client, user1Token);
        var createResponse = await client.PostAsJsonAsync("/api/bookings", new { eventId, seatId });

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
        var (_, eventId, seatId) = await CreateUserAndSeat(factory, setupClient, "setup@test.com");

        // Create two independent clients/users.
        var client1 = factory.CreateClient();
        var client2 = factory.CreateClient();
        var token1 = await TestHelpers.RegisterAndLogin(client1, "user1@test.com");
        var token2 = await TestHelpers.RegisterAndLogin(client2, "user2@test.com");
        TestHelpers.SetBearerToken(client1, token1);
        TestHelpers.SetBearerToken(client2, token2);

        // Start both requests at the same time.
        var task1 = client1.PostAsJsonAsync("/api/bookings", new { eventId, seatId });
        var task2 = client2.PostAsJsonAsync("/api/bookings", new { eventId, seatId });
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

    private static async Task<(string Token, int EventId, int SeatId)>
        CreateUserAndSeat(CustomWebApplicationFactory factory, HttpClient client, string email = "user@test.com")
    {
        var token = await TestHelpers.RegisterAndLoginAsAdmin(factory, client, email);
        TestHelpers.SetBearerToken(client, token);

        var eventResponse = await client.PostAsJsonAsync("/api/events", new { name = "Test Concert", venue = "Test Arena", eventDate = DateTime.UtcNow.AddDays(30) });
        eventResponse.EnsureSuccessStatusCode();
        var eventDto = await eventResponse.Content.ReadFromJsonAsync<EventResponse>() ?? throw new InvalidOperationException("Event was not returned.");

        var payload = new
        {
            seats = new[]
            {
                new
                {
                    row = "A",
                    number = 1,
                    price = 50m
                }
            }
        };
        var seatResponse = await client.PostAsJsonAsync($"/api/events/{eventDto.Id}/seats", payload);
        seatResponse.EnsureSuccessStatusCode();
        var seatDto = await seatResponse.Content.ReadFromJsonAsync<List<SeatDto>>();

        return seatDto!.Count == 0 ? throw new InvalidOperationException("Seat was not returned.") : ((string Token, int EventId, int SeatId))(token, eventDto.Id, seatDto[0].Id);
    }


    private sealed class EventResponse
    {
        public int Id { get; set; }
    }

    private sealed class SeatDto
    {
        public int Id { get; set; }
    }

    private sealed class BookingResponse
    {
        public int Id { get; set; }

        public int SeatId { get; set; } = new();
    }
}