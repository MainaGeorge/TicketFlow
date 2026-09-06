using Microsoft.EntityFrameworkCore;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Tests.Infrastructure.Services;

public partial class TokenServiceTests
{
    public static class TestDatabase
    {
        public static async Task<AppDbContext> CreateAsync()
        {
            var databaseName = $"TicketFlowTests_{Guid.NewGuid():N}";

            var connectionString = $"Server=localhost\\MSSQLSERVER01;Initial Catalog={databaseName};Integrated Security=True;Encrypt=False;TrustServerCertificate=True";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            var context = new AppDbContext(options);

            await context.Database.MigrateAsync();

            return context;
        }
    }
}
