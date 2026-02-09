using System;
using Microsoft.Extensions.DependencyInjection;
using NordAPI.Swish.Webhooks;

namespace NordAPI.Swish.DependencyInjection;
/// <summary>
/// Service collection extensions for registering Swish webhook verification services.
/// </summary>
public static class SwishWebhookServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Swish webhook verifier and exposes options as a concrete instance for DI.
    /// </summary>
    public static ISwishWebhookBuilder AddSwishWebhookVerification(
        this IServiceCollection services,
        Action<SwishWebhookVerifierOptions> configure)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        // IMPORTANT: Verifier depends on SwishWebhookVerifierOptions (not IOptions<>).
        var opts = new SwishWebhookVerifierOptions();
        configure(opts);
        services.AddSingleton(opts);

        services.AddSingleton<SwishWebhookVerifier>();
        return new SwishWebhookBuilder(services);
    }

    /// <summary>
    /// Adds a nonce store depending on environment configuration.
    /// Uses Redis if a connection string is provided, otherwise falls back to in-memory.
    /// </summary>
    public static ISwishWebhookBuilder AddNonceStoreFromEnvironment(
        this ISwishWebhookBuilder builder,
        TimeSpan ttl,
        string prefix = "swish:nonce:")
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        var connectionString =
            Environment.GetEnvironmentVariable("SWISH_REDIS") ??
            Environment.GetEnvironmentVariable("SWISH_REDIS_CONN") ??
            Environment.GetEnvironmentVariable("REDIS_URL");

        // Null-guard: if no Redis connection string, use in-memory store.
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            builder.Services.AddSingleton<ISwishNonceStore, InMemoryNonceStore>();
            return builder;
        }

        builder.Services.AddSingleton<ISwishNonceStore>(_ => new RedisNonceStore(connectionString, prefix));
        return builder;
    }
}
