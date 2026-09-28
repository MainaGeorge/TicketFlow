using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace TicketFlow.Infrastructure.Health;

public sealed class RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = connectionMultiplexer.GetDatabase();

            await database.PingAsync();

            return HealthCheckResult.Healthy("Redis is reachable");
        }
        catch (RedisException exception)
        {
            return HealthCheckResult.Degraded("Redis is unavailable", exception: exception);
        }
    }
}
