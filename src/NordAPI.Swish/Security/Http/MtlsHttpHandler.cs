using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace NordAPI.Swish.Security.Http
{
    /// <summary>
    /// Conditional mTLS handler. Laddar klientcert från:
    /// 1) Explicit X509Certificate2 via konstruktor (om angivet), eller
    /// 2) Miljövariabler: SWISH_PFX_PATH eller SWISH_PFX_BASE64 + SWISH_PFX_PASSWORD.
    /// I dev (DEBUG) kan servercert-validering temporärt vara avslappnad. Detta
    /// MÅSTE tas bort/skärpas innan produktion.
    /// </summary>
    internal sealed class MtlsHttpHandler : HttpClientHandler
    {
        /// <summary>Parameterlös: endast env-styrd mTLS (om env finns).</summary>
        public MtlsHttpHandler()
        {
#if DEBUG
            // DEV ONLY. Skärp innan produktion.
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true;
#endif
            TryAttachCertificateFromEnv();
        }

        /// <summary>
        /// Overload: explicit klientcert via kod (t.ex. injicerat från DI).
        /// </summary>
        public MtlsHttpHandler(X509Certificate2 clientCertificate)
        {
#if DEBUG
            // DEV ONLY. Skärp innan produktion.
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true;
#endif
            if (clientCertificate is null)
                throw new ArgumentNullException(nameof(clientCertificate));

            ClientCertificates.Add(clientCertificate);
        }

        private void TryAttachCertificateFromEnv()
        {
            var pfxPath   = Environment.GetEnvironmentVariable("SWISH_PFX_PATH");
            var pfxBase64 = Environment.GetEnvironmentVariable("SWISH_PFX_BASE64");
            var pfxPass   = Environment.GetEnvironmentVariable("SWISH_PFX_PASSWORD");

            if (string.IsNullOrWhiteSpace(pfxPass))
                return;

            try
            {
                X509Certificate2? cert = null;

                if (!string.IsNullOrWhiteSpace(pfxPath) && System.IO.File.Exists(pfxPath))
                {
                    var raw = System.IO.File.ReadAllBytes(pfxPath);
                    cert = new X509Certificate2(raw, pfxPass, X509KeyStorageFlags.EphemeralKeySet);
                }
                else if (!string.IsNullOrWhiteSpace(pfxBase64))
                {
                    var raw = Convert.FromBase64String(pfxBase64);
                    cert = new X509Certificate2(raw, pfxPass, X509KeyStorageFlags.EphemeralKeySet);
                }

                if (cert is not null)
                {
                    ClientCertificates.Add(cert);
                }
            }
            catch
            {
                // Tyst i dev; vi lägger strukturerad logging senare.
            }
        }
    }
}

