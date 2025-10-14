using System;
using FluentAssertions;
using NordAPI.Swish.Internal;
using Xunit;

namespace NordAPI.Swish.Tests.Internal;

public class RedisEnvTests
{
    [Fact]
    public void TryGetConnection_ReturnsFalse_When_No_Vars_Set()
    {
        var prev1 = Environment.GetEnvironmentVariable("SWISH_REDIS");
        var prev2 = Environment.GetEnvironmentVariable("SWISH_REDIS_CONN");
        var prev3 = Environment.GetEnvironmentVariable("REDIS_URL");
        try
        {
            Environment.SetEnvironmentVariable("SWISH_REDIS", null);
            Environment.SetEnvironmentVariable("SWISH_REDIS_CONN", null);
            Environment.SetEnvironmentVariable("REDIS_URL", null);

            var ok = RedisEnv.TryGetConnection(out var conn);

            ok.Should().BeFalse();
            conn.Should().BeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("SWISH_REDIS", prev1);
            Environment.SetEnvironmentVariable("SWISH_REDIS_CONN", prev2);
            Environment.SetEnvironmentVariable("REDIS_URL", prev3);
        }
    }

    [Fact]
    public void TryGetConnection_Prefers_SWISH_REDIS_Then_CONN_Then_REDIS_URL()
    {
        var prev1 = Environment.GetEnvironmentVariable("SWISH_REDIS");
        var prev2 = Environment.GetEnvironmentVariable("SWISH_REDIS_CONN");
        var prev3 = Environment.GetEnvironmentVariable("REDIS_URL");
        try
        {
            // Start with only REDIS_URL
            Environment.SetEnvironmentVariable("SWISH_REDIS", null);
            Environment.SetEnvironmentVariable("SWISH_REDIS_CONN", null);
            Environment.SetEnvironmentVariable("REDIS_URL", "redis://from_redis_url");
            RedisEnv.TryGetConnection(out var c1).Should().BeTrue();
            c1.Should().Be("redis://from_redis_url");

            // Add SWISH_REDIS_CONN (should override REDIS_URL)
            Environment.SetEnvironmentVariable("SWISH_REDIS_CONN", "redis://from_conn");
            RedisEnv.TryGetConnection(out var c2).Should().BeTrue();
            c2.Should().Be("redis://from_conn");

            // Add SWISH_REDIS (should override both)
            Environment.SetEnvironmentVariable("SWISH_REDIS", "redis://from_primary");
            RedisEnv.TryGetConnection(out var c3).Should().BeTrue();
            c3.Should().Be("redis://from_primary");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SWISH_REDIS", prev1);
            Environment.SetEnvironmentVariable("SWISH_REDIS_CONN", prev2);
            Environment.SetEnvironmentVariable("REDIS_URL", prev3);
        }
    }
}
