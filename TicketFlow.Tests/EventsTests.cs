using System.Net;
using System.Net.Http.Json;

namespace TicketFlow.Tests;

public class EventsTests
{
    [Fact]
    public async Task CreateEvent_WithValidRequest_Returns201()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await TestHelpers.RegisterAndLogin(client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await client.PostAsJsonAsync("/api/events", new { name = "Rock Concert", venue = "London Arena", eventDate = DateTime.UtcNow.AddDays(30) });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithEmptyName_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await TestHelpers.RegisterAndLogin(client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await client.PostAsJsonAsync("/api/events", new { name = "", venue = "London Arena", eventDate = DateTime.UtcNow.AddDays(30) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithEmptyVenue_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await TestHelpers.RegisterAndLogin(client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await client.PostAsJsonAsync("/api/events", new { name = "Rock Concert", venue = "", eventDate = DateTime.UtcNow.AddDays(30) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithPastDate_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await TestHelpers.RegisterAndLogin(client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await client.PostAsJsonAsync("/api/events", new { name = "Old Concert", venue = "London Arena", eventDate = DateTime.UtcNow.AddDays(-1) });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetEvent_WithUnknownId_Returns404()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await TestHelpers.RegisterAndLogin(client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await client.GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_WithCreatedEvent_Returns200()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await TestHelpers.RegisterAndLogin(client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);

        await client.PostAsJsonAsync("/api/events", new { name = "Rock Concert", venue = "London Arena", eventDate = DateTime.UtcNow.AddDays(30) });

        var response = await client.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/events")]
    [InlineData("/api/events/1")]
    public async Task GetEvents_WithoutCredentials_Returns200IfEventsExist(string url)
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await TestHelpers.RegisterAndLogin(client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);

        await client.PostAsJsonAsync("/api/events", new { name = "Rock Concert", venue = "London Arena", eventDate = DateTime.UtcNow.AddDays(30) });

        TestHelpers.SetBearerToken(client, string.Empty);

        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}