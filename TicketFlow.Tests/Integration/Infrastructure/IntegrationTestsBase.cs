namespace TicketFlow.Tests.Integration.Infrastructure;

[Collection(IntegrationTestsCollection.Name)]
public abstract class IntegrationTestsBase(IntegrationTestFixture sqlServer)
{
    private readonly IntegrationTestFixture containerFixture = sqlServer;

    protected CustomWebApplicationFactory CreateFactory()
    {
        return new CustomWebApplicationFactory(containerFixture.GetSqlConnectionString(), containerFixture.GetRedisConnectionString());
    }
}
