using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using StackExchange.Redis;
using TicketFlow.Infrastructure.Health;

namespace TicketFlow.Tests.Infrastructure.HealthChecks;

public class RedisHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenRedisIsReachable_ReturnsHealthy()
    {
        var multiplexer = new Mock<IConnectionMultiplexer>();
        var database = new Mock<IDatabase>();
        var context = new HealthCheckContext();

        database
            .Setup(x => x.PingAsync())
            .ReturnsAsync(TimeSpan.FromSeconds(2));

        multiplexer
            .Setup(x => x.GetDatabase())
            .Returns(database.Object);

        var redisHealthCheck = new RedisHealthCheck(multiplexer.Object);
        var result = await redisHealthCheck.CheckHealthAsync(context);


        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("Redis is reachable", result.Description);
        database.Verify(x => x.PingAsync(), Times.Once);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenRedisThrows_ReturnsDegraded()
    {
        var multiplexer = new Mock<IConnectionMultiplexer>();
        var database = new Mock<IDatabase>();
        var context = new HealthCheckContext();
        var exception = new RedisException("something is wrong with redis");

        database
            .Setup(x => x.PingAsync())
            .ThrowsAsync(exception);

        multiplexer
            .Setup(x => x.GetDatabase())
            .Returns(database.Object);

        var redisHealthCheck = new RedisHealthCheck(multiplexer.Object);
        var result = await redisHealthCheck.CheckHealthAsync(context);


        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("Redis is unavailable", result.Description);
        Assert.Same(exception, result.Exception);

        database.Verify(x => x.PingAsync(), Times.Once);
    }
}
