using System;

namespace NordAPI.Swish.Internal;

internal static class RedisEnv
{
    /// <summary>
    /// Retrieve Redis connection string by priority:
    /// 1) SWISH_REDIS
    /// 2) SWISH_REDIS_CONN
    /// 3) REDIS_URL
    /// Returns true + string if found; otherwise false and <c>null</c>.
    /// </summary>
    internal static bool TryGetConnection(out string? connectionString)
    {
        var a = Environment.GetEnvironmentVariable("SWISH_REDIS");
        var b = Environment.GetEnvironmentVariable("SWISH_REDIS_CONN");
        var c = Environment.GetEnvironmentVariable("REDIS_URL");

        var pick = !string.IsNullOrWhiteSpace(a) ? a
                : !string.IsNullOrWhiteSpace(b) ? b
                : !string.IsNullOrWhiteSpace(c) ? c
                : null;

        if (string.IsNullOrWhiteSpace(pick))
        {
            connectionString = null;           // <- important: null, not ""
            return false;
        }

        connectionString = pick;
        return true;
    }
}
