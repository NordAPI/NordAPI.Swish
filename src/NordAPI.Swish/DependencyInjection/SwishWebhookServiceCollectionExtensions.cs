using System;
using Microsoft.Extensions.DependencyInjection;
using NordAPI.Swish.Internal;

namespace NordAPI.Swish.Webhooks
{
    /// <summary>
    /// DI registration for webhook verification (HMAC + timestamp + nonce).
    /// </summary>
    public static class SwishWebhookServiceCollectionExtensions
    {
        public static ISwishWebhookBuilder AddSwishWebhookVerification(
            this IServiceCollection services,
            Action<SwishWebhookVerifierOptions> configure)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            var opts = new SwishWebhookVerifierOptions();
            configure(opts);
            if (string.IsNullOrWhiteSpace(opts.SharedSecret))
                throw new ArgumentException("SharedSecret must be configured.", nameof(configure));

            services.AddSingleton(opts);
            services.AddSingleton<SwishWebhookVerifier>(); // needs ISwishNonceStore

            return new SwishWebhookBuilder(services);
        }

        public static ISwishWebhookBuilder AddInMemoryNonceStore(this ISwishWebhookBuilder builder)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));
            builder.Services.AddSingleton<ISwishNonceStore, InMemoryNonceStore>();
            return builder;
        }

        public static ISwishWebhookBuilder AddRedisNonceStore(
            this ISwishWebhookBuilder builder,
            Func<IServiceProvider, ISwishNonceStore> factory)
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));
            if (factory is null) throw new ArgumentNullException(nameof(factory));
            builder.Services.AddSingleton(factory);
            return builder;
        }

        /// <summary>
        /// Registers an <see cref="ISwishNonceStore"/> based on environment configuration.
        /// Uses Redis if SWISH_REDIS / SWISH_REDIS_CONN / REDIS_URL is set; otherwise InMemory.
        /// </summary>
        public static ISwishWebhookBuilder AddNonceStoreFromEnvironment(
            this ISwishWebhookBuilder builder,
            TimeSpan inMemoryTtl,
            string redisKeyPrefix = "swish:nonce:")
        {
            if (builder is null) throw new ArgumentNullException(nameof(builder));

            if (RedisEnv.TryGetConnection(out var conn))
            {
                builder.Services.AddSingleton<ISwishNonceStore>(_ =>
                    new RedisNonceStore(conn, redisKeyPrefix));
            }
            else
            {
                builder.Services.AddSingleton<ISwishNonceStore>(_ =>
                    new InMemoryNonceStore(inMemoryTtl));
            }

            return builder;
        }
    }
}




