using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NordAPI.Swish.Webhooks;

/// <summary>
/// Verifies incoming Swish webhook requests by checking HMAC signature, timestamp, and nonce (anti-replay).
/// </summary>
/// <remarks>
/// Header aliases supported:
/// - <c>X-Swish-Timestamp</c> / <c>X-Timestamp</c>
/// - <c>X-Swish-Signature</c> / <c>X-Signature</c>
/// - <c>X-Swish-Nonce</c> / <c>X-Nonce</c>
///
/// Canonical form signed:
/// <code>
/// {timestamp}\n{nonce}\n{body}
/// </code>
///
/// Accepted timestamp formats:
/// - Unix seconds (e.g., 1757962690)
/// </remarks>
public sealed class SwishWebhookVerifier
{
    private static readonly StringComparison Ci = StringComparison.OrdinalIgnoreCase;

    private readonly SwishWebhookVerifierOptions _opt;
    private readonly ISwishNonceStore _nonceStore;

    /// <summary>
    /// Creates a new verifier instance.
    /// </summary>
    /// <param name="options">Verification options, including shared secret and time window.</param>
    /// <param name="nonceStore">Nonce store used for replay protection.</param>
    /// <exception cref="ArgumentException">Thrown if the shared secret is missing.</exception>
    public SwishWebhookVerifier(SwishWebhookVerifierOptions options, ISwishNonceStore nonceStore)
    {
        _opt = options ?? throw new ArgumentNullException(nameof(options));
        _nonceStore = nonceStore ?? throw new ArgumentNullException(nameof(nonceStore));
        if (string.IsNullOrWhiteSpace(_opt.SharedSecret))
            throw new ArgumentException("SharedSecret must be configured.", nameof(options));
    }

    /// <summary>
    /// Verifies the signature, timestamp validity, and nonce replay protection.
    /// </summary>
    /// <param name="body">The raw request body.</param>
    /// <param name="headers">HTTP headers as a key-value collection.</param>
    /// <param name="nowUtc">Current UTC time (for testability).</param>
    /// <returns>A <see cref="VerifyResult"/> indicating success or failure reason.</returns>
    public VerifyResult Verify(string body, IReadOnlyDictionary<string, string> headers, DateTimeOffset nowUtc)
    {
        // 1) Read headers (allow aliases)
        if (!TryGetAny(headers, out var sigB64, _opt.SignatureHeaderName, "X-Swish-Signature", "X-Signature"))
            return VerifyResult.Fail("missing signature header");

        if (!TryGetAny(headers, out var tsStr, _opt.TimestampHeaderName, "X-Swish-Timestamp", "X-Timestamp"))
            return VerifyResult.Fail("missing timestamp header");

        if (!TryGetAny(headers, out var nonce, _opt.NonceHeaderName, "X-Swish-Nonce", "X-Nonce"))
            return VerifyResult.Fail("missing nonce header");

        // 2) Parse timestamp
        if (!TryParseTimestamp(tsStr, out var tsUtc))
            return VerifyResult.Fail("invalid timestamp");

        // 3) Validate clock skew and age
        var skew = (nowUtc - tsUtc).Duration();
        if (skew > _opt.AllowedClockSkew)
            return VerifyResult.Fail("timestamp outside tolerated clock skew");

        if (nowUtc - tsUtc > _opt.MaxMessageAge)
            return VerifyResult.Fail("message too old");

        // 4) Anti-replay: nonce must be new
        var expires = nowUtc.Add(_opt.MaxMessageAge);
        if (!_nonceStore.TryRemember(nonce, expires))
            return VerifyResult.Fail("replay detected (nonce already seen)");

        // 5) Signature verification: Base64(HMACSHA256(secret, "{ts}\n{nonce}\n{body}"))
        var canonical = $"{tsStr}\n{nonce}\n{body}";
        var key = Encoding.UTF8.GetBytes(_opt.SharedSecret);
        var data = Encoding.UTF8.GetBytes(canonical);
        using var hmac = new HMACSHA256(key);
        var mac = hmac.ComputeHash(data);
        var expectedB64 = Convert.ToBase64String(mac);

        if (!ConstantTimeEquals(expectedB64, sigB64))
            return VerifyResult.Fail("signature mismatch");

        return VerifyResult.Ok();
    }
#pragma warning disable S3267 // Intentional: small header set, explicit loop preferred over LINQ for clarity and predictability
    private static bool TryGetAny(IReadOnlyDictionary<string, string> headers, out string value, params string[] names)
    {
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            foreach (var kv in headers)
            {
                if (kv.Key.Equals(name, Ci))
                {
                    value = kv.Value;
                    return true;
                }
            }
        }

        value = "";
        return false;
    }
#pragma warning restore S3267
    private static bool TryParseTimestamp(string tsHeader, out DateTimeOffset tsUtc)
    {
    // STRICT: Unix timestamp in SECONDS only
    if (!long.TryParse(tsHeader, out var seconds))
    {
        tsUtc = default;
        return false;
    }

    try
    {
        tsUtc = DateTimeOffset.FromUnixTimeSeconds(seconds).ToUniversalTime();
        return true;
    }
    catch
    {
        tsUtc = default;
        return false;
    }
    }


    private static bool ConstantTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;

        var diff = 0;
        for (int i = 0; i < ba.Length; i++)
            diff |= ba[i] ^ bb[i];

        return diff == 0;
    }

    /// <summary>
    /// Represents the result of a webhook verification.
    /// </summary>
    /// <param name="Success">Whether the verification succeeded.</param>
    /// <param name="Reason">Optional failure reason.</param>
    public readonly record struct VerifyResult(bool Success, string? Reason)
    {
        /// <summary>
        /// Creates a successful verification result.
        /// </summary>
        public static VerifyResult Ok() => new(true, null);

        /// <summary>
        /// Creates a failed verification result with a reason.
        /// </summary>
        /// <param name="reason">A short failure reason.</param>
        public static VerifyResult Fail(string reason) => new(false, reason);
    }
}
