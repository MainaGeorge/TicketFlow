using System.Net;
using System.Net.Http.Json;

namespace TicketFlow.Tests.Integration;

public class SeatsTests
{
    private static async Task<int> CreateEvent(
        HttpClient client)
    {
        var token = await TestHelpers.RegisterAndLogin(client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);
        var response = await client.PostAsJsonAsync("/api/events", new { name = "Rock Concert", venue = "London Arena", eventDate = DateTime.UtcNow.AddDays(30) });
        response.EnsureSuccessStatusCode();
        var eventDto = await response.Content.ReadFromJsonAsync<EventResponse>();
        return eventDto?.Id ?? throw new InvalidOperationException("Event ID was not returned.");
    }

    [Fact]
    public async Task CreateSeat_WithValidRequest_Returns201()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var eventId = await CreateEvent(client);
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/seats", new { row = "A", number = 1, price = 50 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateSeat_WithZeroNumber_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var eventId = await CreateEvent(client);
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/seats", new { row = "A", number = 0, price = 50 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSeat_WithNegativeNumber_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var eventId = await CreateEvent(client);
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/seats", new { row = "A", number = -1, price = 50 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSeat_WithNegativePrice_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var eventId = await CreateEvent(client);
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/seats", new { row = "A", number = 1, price = -10 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSeat_WithMissingRow_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var eventId = await CreateEvent(client);
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/seats", new { row = "", number = 1, price = 50 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSeat_WithUnknownEvent_Returns404()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = await TestHelpers.RegisterAndLogin(client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);
        var response = await client.PostAsJsonAsync("/api/events/999999/seats", new { row = "A", number = 1, price = 50 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class EventResponse
    {
        public int Id { get; set; }
    }
}