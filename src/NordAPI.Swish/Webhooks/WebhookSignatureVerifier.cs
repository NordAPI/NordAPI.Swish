using System;
using System.Security.Cryptography;
using System.Text;

namespace NordAPI.Swish.Webhooks;

/// <summary>
/// Provides helper methods for computing and validating HMAC signatures for Swish webhook requests.
/// </summary>
public static class WebhookSignatureVerifier
{
    /// <summary>
    /// Computes a Base64-encoded HMAC-SHA256 signature for the given canonical string.
    /// </summary>
    /// <param name="sharedSecret">The shared secret used to compute the signature.</param>
    /// <param name="canonical">The canonical string to sign (usually "{timestamp}\n{nonce}\n{body}").</param>
    /// <returns>The Base64-encoded HMAC-SHA256 signature.</returns>
    /// <exception cref="ArgumentException">Thrown when inputs are null or empty.</exception>
    public static string ComputeSignature(string sharedSecret, string canonical)
    {
        if (string.IsNullOrWhiteSpace(sharedSecret))
            throw new ArgumentException("Shared secret must not be null or empty.", nameof(sharedSecret));

        if (string.IsNullOrEmpty(canonical))
            throw new ArgumentException("Canonical string must not be null or empty.", nameof(canonical));

        var keyBytes = Encoding.UTF8.GetBytes(sharedSecret);
        var dataBytes = Encoding.UTF8.GetBytes(canonical);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Validates that the provided signature matches the expected HMAC value for the given canonical input.
    /// Comparison is done in constant time to prevent timing attacks.
    /// </summary>
    /// <param name="sharedSecret">The shared secret used to verify the signature.</param>
    /// <param name="canonical">The canonical string to verify.</param>
    /// <param name="providedSignatureB64">The Base64-encoded signature provided in the request header.</param>
    /// <returns><c>true</c> if the signature is valid; otherwise <c>false</c>.</returns>
    public static bool VerifySignature(string sharedSecret, string canonical, string providedSignatureB64)
    {
        if (string.IsNullOrWhiteSpace(providedSignatureB64))
            return false;

        var expected = ComputeSignature(sharedSecret, canonical);
        return ConstantTimeEquals(expected, providedSignatureB64);
    }

    /// <summary>
    /// Performs constant-time comparison of two Base64-encoded signatures to prevent timing side-channel leaks.
    /// </summary>
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
}
