using Testcontainers.MsSql;
using Testcontainers.Redis;

namespace TicketFlow.Tests.Integration.Infrastructure;

public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer;
    private readonly RedisContainer _redisContainer;
    public IntegrationTestFixture()
    {
        _sqlContainer = new MsSqlBuilder(image: "mcr.microsoft.com/mssql/server:2022-latest").Build();
        _redisContainer = new RedisBuilder(image: "redis:latest").Build();
    }

    public string GetSqlConnectionString() => _sqlContainer.GetConnectionString();

    public string GetRedisConnectionString() => _redisContainer.GetConnectionString();

    public async Task DisposeAsync()
    { 
        await Task
             .WhenAll
            (
                _sqlContainer.DisposeAsync().AsTask(),
                _redisContainer.DisposeAsync().AsTask()
            );
    }

    public async Task InitializeAsync()
    {
        await Task
            .WhenAll
            (
                _sqlContainer.StartAsync(),
                _redisContainer.StartAsync()
            );
    }
}
