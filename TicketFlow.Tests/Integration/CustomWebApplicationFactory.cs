using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Tests.Integration;

public class CustomWebApplicationFactory(string sqlConnectionString, string redisConnectionString) : WebApplicationFactory<Program>
{
    private readonly string _sqlConnectionString = sqlConnectionString;
    private readonly string _redisConnectionString = redisConnectionString;
    private readonly string _databaseName = $"TicketFlow_Test_{Guid.NewGuid():N}";
    private string? _testDatabaseConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(_sqlConnectionString)
            {
                InitialCatalog = _databaseName
            };

            var connectionString = connectionStringBuilder.ConnectionString;
            _testDatabaseConnectionString = connectionString;

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Redis:ConnectionString"] = _redisConnectionString
            });
        });

        builder.ConfigureServices(services =>
        {
            using var scope = services.BuildServiceProvider().CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Database.Migrate();

            DatabaseMigrationExtensions.SeedRolesAsync(scope.ServiceProvider).GetAwaiter().GetResult();
        });
    }
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        SqlConnection.ClearAllPools();

        if (_testDatabaseConnectionString is null)
        {
            return;
        }

        var connectionStringBuilder =
            new SqlConnectionStringBuilder(_testDatabaseConnectionString)
            {
                InitialCatalog = "master"
            };

        await using var connection = new SqlConnection(connectionStringBuilder.ConnectionString);

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();

        command.CommandText = 
        $"""
            ALTER DATABASE [{_databaseName}]
            SET SINGLE_USER
            WITH ROLLBACK IMMEDIATE;

            DROP DATABASE [{_databaseName}];
        """;

        await command.ExecuteNonQueryAsync();
    }
}