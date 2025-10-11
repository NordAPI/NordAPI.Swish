using System;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace NordAPI.Swish.Webhooks
{
    public sealed class RedisNonceStore : ISwishNonceStore
    {
        private readonly IDatabase _db;
        private readonly string _keyPrefix;
        private static readonly TimeSpan MaxTtl = TimeSpan.FromDays(7);

        public RedisNonceStore(IConnectionMultiplexer mux, string keyPrefix = "swish:nonce:")
        {
            if (mux is null) throw new ArgumentNullException(nameof(mux));
            _db = mux.GetDatabase();
            _keyPrefix = string.IsNullOrWhiteSpace(keyPrefix) ? "swish:nonce:" : keyPrefix;
        }

        private static TimeSpan ComputeTtl(DateTimeOffset expiresAtUtc)
        {
            var now = DateTimeOffset.UtcNow;
            var ttl = expiresAtUtc.ToUniversalTime() - now;
            if (ttl < TimeSpan.Zero) ttl = TimeSpan.Zero;
            if (ttl > MaxTtl) ttl = MaxTtl;
            return ttl;
        }

        private string K(string nonce) => _keyPrefix + nonce;

        public async Task<bool> TryRememberAsync(string nonce, DateTimeOffset expiresAtUtc, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(nonce))
                throw new ArgumentException("Nonce cannot be null or empty.", nameof(nonce));

            var ttl = ComputeTtl(expiresAtUtc);
            var added = await _db.StringSetAsync(
                key: K(nonce),
                value: DateTimeOffset.UtcNow.ToString("O"),
                expiry: ttl,
                when: When.NotExists
            ).ConfigureAwait(false);

            return added;
        }
    }
}
