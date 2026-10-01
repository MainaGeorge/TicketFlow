namespace TicketFlow.Tests.Integration.Infrastructure;

[CollectionDefinition(Name)]
public class IntegrationTestsCollection : ICollectionFixture<IntegrationTestFixture>
{
    public const string Name = "IntegrationTests";
}
