using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Infrastructure.Caching;
using TicketFlow.Tests.Integration.Infrastructure;

namespace TicketFlow.Tests.Integration.Caching;

[Collection(IntegrationTestsCollection.Name)]
public class RedisCacheServiceTests : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly RedisCacheService _cacheService;

    public RedisCacheServiceTests(IntegrationTestFixture fixture)
    {
        var services = new ServiceCollection();
        services.AddStackExchangeRedisCache(options => options.Configuration = fixture.GetRedisConnectionString());
        _provider = services.BuildServiceProvider(validateScopes: true);
        var distributedCache = _provider.GetRequiredService<IDistributedCache>();
        _cacheService = new RedisCacheService(distributedCache);
    }

    [Fact]
    public async Task GetAsync_WhenValueWasCached_ReturnsDeserializedValue()
    {
        var key = $"test:{Guid.NewGuid()}";
        var value = new TestCacheValue(1, "TicketFlow");

        await _cacheService.SetAsync(key, value, TimeSpan.FromMinutes(5));

        var retrievedValue = await _cacheService.GetAsync<TestCacheValue>(key);

        Assert.NotNull(retrievedValue);
        Assert.Equal(value.Name, retrievedValue.Name);
        Assert.Equal(value.Id, retrievedValue.Id);
    }

    [Fact]
    public async Task GetAsync_WhenValueIsNotCached_ReturnsNull()
    {
        var key = $"test:{Guid.NewGuid()}";
        var retrievedValue = await _cacheService.GetAsync<TestCacheValue>(key);

        Assert.Null(retrievedValue);
    }

    [Fact]
    public async Task RemoveAsync_WhenValueIsCached_RemovesValue()
    {
        var key = $"test:{Guid.NewGuid()}";
        var services = new ServiceCollection();
        var value = new TestCacheValue(1, "TicketFlow");

        await _cacheService.SetAsync(key, value, TimeSpan.FromMinutes(5));
        var retrievedValueBeforeRemoval = await _cacheService.GetAsync<TestCacheValue>(key);
        Assert.NotNull(retrievedValueBeforeRemoval);

        await _cacheService.RemoveAsync(key);

        var retrievedValueAfterRemoval = await _cacheService.GetAsync<TestCacheValue>(key);
        Assert.Null(retrievedValueAfterRemoval);
    }

    [Fact]
    public async Task GetAsync_WhenCachedValueHasExpired_ReturnsNull()
    {
        var key = $"test:{Guid.NewGuid()}";
        var value = new TestCacheValue(1, "TicketFlow");

        await _cacheService.SetAsync(key, value, TimeSpan.FromSeconds(1));

        var retrievedValueBeforeExpiration = await _cacheService.GetAsync<TestCacheValue>(key);
        Assert.NotNull(retrievedValueBeforeExpiration);

        await Task.Delay(TimeSpan.FromSeconds(1.5));

        var retrievedValueAfterExpiration = await _cacheService.GetAsync<TestCacheValue>(key);
        Assert.Null(retrievedValueAfterExpiration);
    }

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    private sealed record TestCacheValue(int Id, string Name);
}
