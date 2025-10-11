using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace NordAPI.Swish.Webhooks;

/// <summary>
/// Provides extension methods for registering and using <see cref="ISwishNonceStore"/> implementations.
/// Includes backward-compatibility helpers for legacy method shapes.
/// </summary>
public static partial class NonceStoreExtensions
{
    /// <summary>
    /// Backward-compatible legacy helper: returns <c>true</c> if the nonce has already been seen recently.
    /// </summary>
    public static async Task<bool> SeenRecentlyAsync(
        this ISwishNonceStore store,
        string nonce,
        DateTimeOffset timestamp,
        TimeSpan window,
        CancellationToken ct = default)
    {
        var ok = await store.TryRememberAsync(nonce, timestamp.Add(window), ct);
        return !ok; // false => already exists (replay)
    }

    /// <summary>
    /// Legacy synchronous wrapper for <see cref="ISwishNonceStore.TryRememberAsync"/>.
    /// </summary>
    public static bool TryRemember(
        this ISwishNonceStore store,
        string nonce,
        DateTimeOffset expiresAtUtc,
        CancellationToken ct = default)
    {
        return store.TryRememberAsync(nonce, expiresAtUtc, ct).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Registers a Redis-backed <see cref="ISwishNonceStore"/> in the service collection.
    /// Reads connection string from <c>SWISH_REDIS</c> (or defaults to localhost).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">The configuration provider.</param>
    /// <param name="keyPrefix">Optional key prefix used for Redis storage.</param>
    public static IServiceCollection AddRedisNonceStore(
        this IServiceCollection services,
        IConfiguration config,
        string keyPrefix = "swish:nonce:")
    {
        var connStr = config["SWISH_REDIS"];
        if (string.IsNullOrWhiteSpace(connStr))
            connStr = "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connStr));
        services.AddSingleton<ISwishNonceStore>(sp =>
        {
            var mux = sp.GetRequiredService<IConnectionMultiplexer>();
            return new RedisNonceStore(mux, keyPrefix);
        });

        return services;
    }
}

