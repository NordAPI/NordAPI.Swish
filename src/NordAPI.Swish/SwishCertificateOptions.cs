using System.Security.Cryptography.X509Certificates;

namespace NordAPI.Swish;

public sealed class SwishCertificateOptions
{
    /// <summary>Full path till en PFX-fil (lokalt/dev).</summary>
    public string? PfxPath { get; set; }

    /// <summary>Lösenord för PFX-filen.</summary>
    public string? PfxPassword { get; set; }

    /// <summary>Om du redan har laddat certet externt.</summary>
    public X509Certificate2? Certificate { get; set; }

    /// <summary>
    /// Om sant: tillåt även privata nycklar i icke-exportabla contexts (t.ex. CNG).
    /// </summary>
    public bool AllowInvalidChainForDev { get; set; } = false; // endast för lokal dev
}
