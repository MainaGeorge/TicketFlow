using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;
using System.Text.Json;
using TicketFlow.Presentation.Health;
using TicketFlow.Tests.Integration.Infrastructure;

namespace TicketFlow.Tests.Presentation.HealthChecks;

public class HealthCheckEndpointTests(IntegrationTestFixture sqlServer) : IntegrationTestsBase(sqlServer)
{
    [Fact]
    public async Task HealthLive_WhenApplicationIsRunning_ReturnsOk()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WriteResponse_WhenGivenHealthReport_WritesExpectedJson()
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            { "sql", new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(44.5), null, null) },
            { "redis", new HealthReportEntry(HealthStatus.Degraded, "Redis is unavailable", TimeSpan.FromMilliseconds(30.5), new Exception("SECRET INTERNAL DETAILS"), null) },
            { "masstransit-bus", new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(20.5), null, null) },
        };

        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(100));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await HealthCheckResponseWriter.WriteResponse(context, report);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("Degraded", root.GetProperty("status").GetString());

        var checks = root.GetProperty("checks");
        Assert.Equal(3, checks.GetArrayLength());

        var sql = checks.EnumerateArray().Single(x => x.GetProperty("name").GetString() == "sql");

        Assert.Equal("Healthy", sql.GetProperty("status").GetString());
        Assert.Equal(44.5, sql.GetProperty("duration").GetDouble());
        Assert.Equal(JsonValueKind.Null, sql.GetProperty("description").ValueKind);

        var redis = checks.EnumerateArray().Single(x => x.GetProperty("name").GetString() == "redis");
        Assert.Equal("Degraded", redis.GetProperty("status").GetString());
        Assert.DoesNotContain("SECRET INTERNAL DETAILS", json);
    }
}
