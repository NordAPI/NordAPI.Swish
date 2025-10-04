using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using NordAPI.Swish.Security.Http;
using Xunit;

public class MtlsHandlerTests
{
    [Fact]
    public void Create_Returns_Handler_And_Does_Not_Throw_When_No_Env()
    {
        // Ensure env is not set (test is hermetic)
        var oldPath = Environment.GetEnvironmentVariable("SWISH_PFX_PATH");
        var oldPass = Environment.GetEnvironmentVariable("SWISH_PFX_PASS");
        Environment.SetEnvironmentVariable("SWISH_PFX_PATH", null);
        Environment.SetEnvironmentVariable("SWISH_PFX_PASS", null);

        try
        {
            var h = SwishMtlsHandlerFactory.Create();
            h.Should().NotBeNull();

            // We expect SocketsHttpHandler without client certs
            var sockets = h as SocketsHttpHandler;
            sockets.Should().NotBeNull();
            (sockets!.SslOptions.ClientCertificates is null || sockets.SslOptions.ClientCertificates.Count == 0)
                .Should().BeTrue("no env -> no client certificate");
        }
        finally
        {
            // restore env for other tests
            Environment.SetEnvironmentVariable("SWISH_PFX_PATH", oldPath);
            Environment.SetEnvironmentVariable("SWISH_PFX_PASS", oldPass);
        }
    }

    [Fact]
    public void Create_Uses_ClientCert_If_Env_Is_Present_OR_Skips_Assertion()
    {
        var p = Environment.GetEnvironmentVariable("SWISH_PFX_PATH");
        var s = Environment.GetEnvironmentVariable("SWISH_PFX_PASS");

        var h = SwishMtlsHandlerFactory.Create();
        var sockets = h as SocketsHttpHandler;
        sockets.Should().NotBeNull();

        if (!string.IsNullOrWhiteSpace(p) && !string.IsNullOrWhiteSpace(s))
        {
            sockets!.SslOptions.ClientCertificates.Should().NotBeNull();
            sockets.SslOptions.ClientCertificates!.Count.Should().BeGreaterThan(0,
                "when both env vars are present, the client certificate should be loaded");
        }
        else
        {
            // When env is missing, we only assert that no certs are present.
            (sockets!.SslOptions.ClientCertificates is null || sockets.SslOptions.ClientCertificates.Count == 0)
                .Should().BeTrue("no env -> no client certificate");
        }
    }
}
