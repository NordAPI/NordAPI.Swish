using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace NordAPI.Swish.Webhooks
{
    public static class RedisNonceServiceCollectionExtensions
    {
        public static IServiceCollection AddSwishRedisNonceStore(this IServiceCollection services, IConfiguration config, string keyPrefix = "swish:nonce:")
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
}
