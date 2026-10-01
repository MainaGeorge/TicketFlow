using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using TicketFlow.Application.Abstractions.Caching;

namespace TicketFlow.Infrastructure.Caching;

public sealed class RedisCacheService(IDistributedCache cache) : ICacheService
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var cachedStringValue = await cache.GetStringAsync(key, cancellationToken);

        if (cachedStringValue is null)
            return default;

        return JsonSerializer.Deserialize<T>(cachedStringValue);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(key, cancellationToken);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        var serializedValue = JsonSerializer.Serialize(value);
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration };
        await cache.SetStringAsync(key, serializedValue, options, cancellationToken);
    }
}
