using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TransactionAggregator.Application.Common.Interfaces;

using StackExchange.Redis;

namespace TransactionAggregator.Infrastructure.Caching;

/// <summary>
/// Redis-backed distributed cache service.
/// Wraps IDistributedCache with JSON serialization and the get-or-set pattern.
/// </summary>
public sealed class RedisCacheService(
    IDistributedCache cache,
    IConnectionMultiplexer redis,
    ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var bytes = await cache.GetAsync(key, ct);
            if (bytes is null) return default;

            return JsonSerializer.Deserialize<T>(bytes, SerializerOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache GET failed for key '{Key}' — falling through", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(5)
            };
            await cache.SetAsync(key, bytes, options, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache SET failed for key '{Key}'", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try { await cache.RemoveAsync(key, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Cache REMOVE failed for key '{Key}'", key); }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Removing cache keys with prefix: {Prefix}", prefix);
            
            // Note: AddStackExchangeRedisCache uses InstanceName prefix. We match it here.
            var searchPattern = $"TransactionAgg:{prefix}*";
            var endpoints = redis.GetEndPoints();
            var server = redis.GetServer(endpoints.First());
            
            // Using SCAN command (via Keys()) to avoid blocking the Redis server
            var keys = server.Keys(pattern: searchPattern).ToArray();
            
            if (keys.Length > 0)
            {
                var db = redis.GetDatabase();
                await db.KeyDeleteAsync(keys);
                logger.LogInformation("Removed {Count} cache keys matching prefix: {Prefix}", keys.Length, prefix);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove cache keys by prefix '{Prefix}'", prefix);
        }
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null) return cached;

        var value = await factory();
        await SetAsync(key, value, expiry, ct);
        return value;
    }
}
