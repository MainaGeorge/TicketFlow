using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TicketFlow.Tests.Integration;

public class SeatEndpointsTests
{
    private static async Task<int> CreateEvent(
        CustomWebApplicationFactory factory, HttpClient client)
    {
        var token = await TestHelpers.RegisterAndLoginAsAdmin(factory, client, "test@user.com");
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
        var eventId = await CreateEvent(factory, client);
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
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/seats", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateSeat_WithZeroNumber_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var eventId = await CreateEvent(factory, client);
        var payload = new
        {
            seats = new[]
            {
                new
                {
                    row = "A",
                    number = 0,
                    price = 50m
                }
            }
        };
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/seats", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSeat_WithNegativeNumber_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var eventId = await CreateEvent(factory, client);
        var payload = new
        {
            seats = new[]
            {
                new
                {
                    row = "A",
                    number = -1,
                    price = 50m
                }
            }
        };
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/seats", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSeat_WithNegativePrice_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var eventId = await CreateEvent(factory, client);
        var payload = new
        {
            seats = new[]
            {
                new
                {
                    row = "",
                    number = 1,
                    price = -50m
                }
            }
        };
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/seats", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSeat_WithMissingRow_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var eventId = await CreateEvent(factory, client);
        var payload = new
        {
            seats = new[]
            {
                new
                {
                    row = "",
                    number = 1,
                    price = 50m
                }
            }
        };
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/seats", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSeat_WithUnknownEvent_Returns404()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = await TestHelpers.RegisterAndLoginAsAdmin(factory, client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);
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
        var response = await client.PostAsJsonAsync("/api/events/999999/seats", payload);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WhenPriceIsInvalid_ReturnsBadRequestWithValidationErrors()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var eventId = await CreateEvent(factory, client);
        var payload = new
        {
            seats = new[]
            {
                new
                {
                    row = "A",
                    number = 1,
                    price = 0m
                }
            }
        };
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/seats", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.NotNull(problemDetails);
        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
        Assert.True(problemDetails.Errors.ContainsKey("Seats[0].Price"));
        Assert.NotEmpty(problemDetails.Errors["Seats[0].Price"]);
    }

    private sealed class EventResponse
    {
        public int Id { get; set; }
    }
}