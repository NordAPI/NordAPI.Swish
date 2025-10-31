using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NordAPI.Swish.Tests.Webhooks;

public class RedisEnvAlias_E2ETests
{
    [Theory]
    [InlineData("SWISH_REDIS", "localhost:6379")]
    [InlineData("REDIS_URL", "localhost:6379")]
    [InlineData("SWISH_REDIS_CONN", "localhost:6379")]
    public async Task App_Should_Start_When_RedisEnv_Set(string envName, string value)
    {
        Environment.SetEnvironmentVariable(envName, value);

        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var resp = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        Environment.SetEnvironmentVariable(envName, null);
    }

    [Fact]
    public async Task App_Should_FallBack_To_InMemory_When_No_Env_Set()
    {
        Environment.SetEnvironmentVariable("SWISH_REDIS", null);
        Environment.SetEnvironmentVariable("REDIS_URL", null);
        Environment.SetEnvironmentVariable("SWISH_REDIS_CONN", null);

        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var resp = await client.GetAsync("/di-check");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}