using System.Net;
using System.Net.Http.Json;
using TicketFlow.Tests.Integration.Infrastructure;

namespace TicketFlow.Tests.Integration;
 
public class EventEndpointTests(IntegrationTestFixture sqlServerFixture) : IntegrationTestsBase(sqlServerFixture)
{
    [Fact]
    public async Task CreateEvent_WithoutAuthentication_Returns401()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/events", new { name = "Rock Concert", venue = "London Arena", eventDate = DateTime.UtcNow.AddDays(30) });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_AsUser_Returns403()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var token = await TestHelpers.RegisterAndLogin(client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await client.PostAsJsonAsync("/api/events", new { name = "Rock Concert", venue = "London Arena", eventDate = DateTime.UtcNow.AddDays(30) });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithValidRequest_Returns201()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var token = await TestHelpers.RegisterAndLoginAsAdmin(factory, client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await client.PostAsJsonAsync("/api/events", new { name = "Rock Concert", venue = "London Arena", eventDate = DateTime.UtcNow.AddDays(30) });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithEmptyName_Returns400()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var token = await TestHelpers.RegisterAndLoginAsAdmin(factory, client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await client.PostAsJsonAsync("/api/events", new { name = "", venue = "London Arena", eventDate = DateTime.UtcNow.AddDays(30) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithEmptyVenue_Returns400()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var token = await TestHelpers.RegisterAndLoginAsAdmin(factory, client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await client.PostAsJsonAsync("/api/events", new { name = "Rock Concert", venue = "", eventDate = DateTime.UtcNow.AddDays(30) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithPastDate_Returns400()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var token = await TestHelpers.RegisterAndLoginAsAdmin(factory, client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);

        var response = await client.PostAsJsonAsync("/api/events", new { name = "Old Concert", venue = "London Arena", eventDate = DateTime.UtcNow.AddDays(-1) });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetEvent_WithUnknownId_Returns404()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_WithCreatedEvent_Returns200()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var token = await TestHelpers.RegisterAndLoginAsAdmin(factory, client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);

        var createdResponse = await client.PostAsJsonAsync("/api/events", new { name = "Rock Concert", venue = "London Arena", eventDate = DateTime.UtcNow.AddDays(30) });

        createdResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/events")]
    [InlineData("/api/events/1")]
    public async Task GetEvents_WithoutCredentials_Returns200IfEventsExist(string url)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var token = await TestHelpers.RegisterAndLoginAsAdmin(factory, client, "test@user.com");
        TestHelpers.SetBearerToken(client, token);

        var createEventResult = await client.PostAsJsonAsync("/api/events", new { name = "Rock Concert", venue = "London Arena", eventDate = DateTime.UtcNow.AddDays(30) });
        createEventResult.EnsureSuccessStatusCode();

        TestHelpers.SetBearerToken(client, string.Empty);

        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}