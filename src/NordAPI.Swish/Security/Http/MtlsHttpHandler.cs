using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace NordAPI.Swish.Security.Http;

/// <summary>
/// HttpClientHandler som bifogar klientcertifikat (mTLS).
/// OBS: Dev-läget (skippa servercert-validering) är tillåtet endast i DEBUG.
/// </summary>
public sealed class MtlsHttpHandler : HttpClientHandler
{
    public MtlsHttpHandler(X509Certificate2 certificate, bool allowInvalidChainForDev = false)
    {
        if (certificate is null) throw new ArgumentNullException(nameof(certificate));

#if !DEBUG
        // Extra säkerhet: blockera om någon försöker tillåta dev-läget i Release-builds
        if (allowInvalidChainForDev)
            throw new InvalidOperationException("AllowInvalidChainForDev är inte tillåtet i Release-builds.");
#endif

        ClientCertificates.Add(certificate);

        // Endast i lokal/dev: acceptera vilket servercert som helst (t.ex. self-signed/stubbar)
        if (allowInvalidChainForDev)
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
    }
}
