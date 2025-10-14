using System;
using System.Threading.Tasks;
using NordAPI.Swish.Webhooks;
using Xunit;

namespace NordAPI.Swish.Tests
{
    /// <summary>
    /// Integration-like check for RedisNonceStore.
    /// Skips unless a valid Redis connection string is present.
    /// </summary>
    public class RedisNonceStoreTests
    {
        [Fact]
        public async Task TryRememberAsync_Works_When_Redis_Configured()
        {
            var conn =
                Environment.GetEnvironmentVariable("SWISH_REDIS") ??
                Environment.GetEnvironmentVariable("REDIS_URL") ??
                Environment.GetEnvironmentVariable("SWISH_REDIS_CONN");

            // No value => skip
            if (string.IsNullOrWhiteSpace(conn))
            {
                Console.WriteLine("Redis not configured. Set SWISH_REDIS (or REDIS_URL / SWISH_REDIS_CONN) to run this test.");
                return;
            }

            // Basic sanity: must not be empty/placeholder and should resemble a connection string
            var trimmed = conn.Trim();
            if (trimmed.Length == 0 || (!trimmed.Contains(":") && !trimmed.Contains("=")))
            {
                Console.WriteLine($"Redis connection string looks invalid ('{conn}'). Skipping test.");
                return;
            }

            // Try constructing; if invalid, skip rather than fail CI
            RedisNonceStore store;
            try
            {
                store = new RedisNonceStore(trimmed, "test:nonce:");
            }
            catch (ArgumentException ex) when (ex.ParamName == "config")
            {
                Console.WriteLine("Redis connection string could not be parsed. Skipping test.");
                return;
            }

            using (store)
            {
                var nonce = Guid.NewGuid().ToString("N");
                var exp = DateTimeOffset.UtcNow.AddSeconds(5);

                var first  = await store.TryRememberAsync(nonce, exp);
                var second = await store.TryRememberAsync(nonce, exp);

                Assert.True(first,  "first attempt should store the nonce");
                Assert.False(second, "second attempt should be detected as replay");
            }
        }
    }
}
