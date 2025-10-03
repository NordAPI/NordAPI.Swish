using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace NordAPI.Swish.Security.Http
{
    /// <summary>
    /// Creates an HttpMessageHandler that enables client-certificate (mTLS)
    /// when SWISH_PFX_PATH and SWISH_PFX_PASS are present. Falls back to a
    /// plain SocketsHttpHandler when they are missing (no cert required).
    /// </summary>
    internal static class SwishMtlsHandlerFactory
    {
        public static HttpMessageHandler Create()
        {
            var pfxPath = Environment.GetEnvironmentVariable("SWISH_PFX_PATH");
            var pfxPass = Environment.GetEnvironmentVariable("SWISH_PFX_PASS");

            var handler = new SocketsHttpHandler();

            // No cert provided -> fallback (works in CI/Debug without certs)
            if (string.IsNullOrWhiteSpace(pfxPath) || string.IsNullOrWhiteSpace(pfxPass))
                return handler;

            // Load PFX and enable client certificate authentication
            var cert = new X509Certificate2(
                pfxPath,
                pfxPass,
                X509KeyStorageFlags.EphemeralKeySet);

            handler.SslOptions.ClientCertificates = new X509CertificateCollection { cert };

#if DEBUG
            // DEV ONLY: permissive validation while developing locally.
            // In Release, this is NOT included.
            handler.SslOptions.RemoteCertificateValidationCallback = (s, c, ch, e) => true;
#endif
            return handler;
        }
    }
}
