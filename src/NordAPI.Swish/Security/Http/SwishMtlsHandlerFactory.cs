using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace NordAPI.Swish.Security.Http
{
    /// <summary>
    /// Builds the primary HttpMessageHandler used by Swish clients.
    /// - If SWISH_PFX_PATH + SWISH_PFX_PASS are set -> mTLS enabled with that certificate.
    /// - Otherwise -> plain SocketsHttpHandler (no client certificate).
    ///
    /// DEBUG: relaxed server certificate validation (for local/dev only).
    /// RELEASE: strict default validation (no bypass).
    /// </summary>
    public static class SwishMtlsHandlerFactory
    {
        public static HttpMessageHandler Create()
        {
            var pfxPath = Environment.GetEnvironmentVariable("SWISH_PFX_PATH");
            var pfxPass = Environment.GetEnvironmentVariable("SWISH_PFX_PASS");

            var handler = new SocketsHttpHandler();

            // DEBUG: dev convenience (never in Release)
#if DEBUG
            handler.SslOptions.RemoteCertificateValidationCallback = static (_, __, ___, ____) => true;
#endif

            if (!string.IsNullOrWhiteSpace(pfxPath) && !string.IsNullOrWhiteSpace(pfxPass))
            {
                // Load PFX from disk (do NOT commit certs to the repo!)
                var clientCert = new X509Certificate2(
                    pfxPath,
                    pfxPass,
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);

                handler.SslOptions.ClientCertificates ??= new X509CertificateCollection();
                handler.SslOptions.ClientCertificates.Add(clientCert);
            }

            return handler;
        }
    }
}

