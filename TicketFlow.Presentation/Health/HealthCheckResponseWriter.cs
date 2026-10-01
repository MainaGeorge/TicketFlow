using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace TicketFlow.Presentation.Health;

public class HealthCheckResponseWriter
{
    public static async Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var checkResults = report.Entries
            .Select(entry => new HealthCheckResult(
                entry.Key,
                entry.Value.Status.ToString(),
                entry.Value.Description,
                entry.Value.Duration.TotalMilliseconds))
            .ToList();

        var response = new HealthCheckResponseResult(
            report.Status.ToString(),
            checkResults);

        var payload = JsonSerializer.Serialize(response, JsonOptions);

        await context.Response.WriteAsync(payload);
    }
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private record HealthCheckResponseResult(string Status, List<HealthCheckResult> Checks);
    private record HealthCheckResult(string Name, string Status, string? Description, double Duration);
}
