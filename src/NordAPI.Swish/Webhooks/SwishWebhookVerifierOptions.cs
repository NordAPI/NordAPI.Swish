using System;

namespace NordAPI.Swish.Webhooks;

/// <summary>
/// Configuration options for <see cref="SwishWebhookVerifier"/>.
/// Controls timestamp tolerance, nonce lifetime, and header names.
/// </summary>
public sealed class SwishWebhookVerifierOptions
{
    /// <summary>
    /// The shared secret used for computing and verifying the HMAC signature.
    /// </summary>
    public string SharedSecret { get; set; } = string.Empty;

    /// <summary>
    /// The maximum allowed clock skew between sender and receiver.
    /// Default is ±5 minutes.
    /// </summary>
    public TimeSpan AllowedClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The maximum age a webhook message is accepted for.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan MaxMessageAge { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The name of the header containing the Base64-encoded HMAC signature.
    /// Default is <c>X-Swish-Signature</c>.
    /// </summary>
    public string SignatureHeaderName { get; set; } = "X-Swish-Signature";

    /// <summary>
    /// The name of the header containing the Unix or ISO timestamp.
    /// Default is <c>X-Swish-Timestamp</c>.
    /// </summary>
    public string TimestampHeaderName { get; set; } = "X-Swish-Timestamp";

    /// <summary>
    /// The name of the header containing the unique nonce for replay protection.
    /// Default is <c>X-Swish-Nonce</c>.
    /// </summary>
    public string NonceHeaderName { get; set; } = "X-Swish-Nonce";
}

