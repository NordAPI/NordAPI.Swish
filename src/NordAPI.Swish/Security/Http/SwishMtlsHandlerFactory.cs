using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace NordAPI.Swish.Security.Http;

/// <summary>
/// Factory for creating the primary HTTP handler used by the Swish client.
/// </summary>
public static class SwishMtlsHandlerFactory
{
    /// <summary>
    /// Creates a configured <see cref="HttpMessageHandler"/> for Swish HTTP calls.
    /// If environment variables are present, mTLS is enabled with the client certificate.
/// </summary>
    public static HttpMessageHandler Create()
    {
        // Primary names used across README and tests; accept legacy PASS as fallback.
        var pfxPath = Environment.GetEnvironmentVariable("SWISH_PFX_PATH");
        var pfxPassword = Environment.GetEnvironmentVariable("SWISH_PFX_PASSWORD")
            ?? Environment.GetEnvironmentVariable("SWISH_PFX_PASS"); // legacy fallback

        var handler = new SocketsHttpHandler();

#if DEBUG
#pragma warning disable S4830 // DEV ONLY: relaxed server validation in Debug; NEVER in Release.
        // Dev convenience ONLY in Debug: accept any server certificate chain.
        // Never ship this in Release.
        handler.SslOptions.RemoteCertificateValidationCallback =
            static (_, __, ___, ____) => true;
#pragma warning restore S4830
#endif

        // Enable mTLS only if both path and password are provided.
        if (!string.IsNullOrWhiteSpace(pfxPath) && !string.IsNullOrWhiteSpace(pfxPassword))
        {
            // Load PFX from disk. Do not commit certificates to source control.
            // EphemeralKeySet avoids persisting private keys on disk; MachineKeySet helps on CI agents.
            var clientCert = new X509Certificate2(
                pfxPath,
                pfxPassword,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);

            handler.SslOptions.ClientCertificates ??= new X509CertificateCollection();
            handler.SslOptions.ClientCertificates.Add(clientCert);
        }

        return handler;
    }
}
