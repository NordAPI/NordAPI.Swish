// src/NordAPI.Swish/Security/Webhooks/SwishWebhookVerifier.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NordAPI.Swish.Security.Webhooks;

public sealed class SwishWebhookVerifier
{
    private readonly SwishWebhookVerifierOptions _opt;
    private readonly ISwishNonceStore _nonceStore;

    public SwishWebhookVerifier(SwishWebhookVerifierOptions options, ISwishNonceStore nonceStore)
    {
        _opt = options ?? throw new ArgumentNullException(nameof(options));
        _nonceStore = nonceStore ?? throw new ArgumentNullException(nameof(nonceStore));

        if (string.IsNullOrWhiteSpace(_opt.SharedSecret))
            throw new ArgumentException("SharedSecret måste vara satt.", nameof(options));

        // Rimliga defaultar om de inte är satta i options
        if (_opt.AllowedClockSkew <= TimeSpan.Zero)
            _opt.AllowedClockSkew = TimeSpan.FromMinutes(5);
        if (_opt.MaxMessageAge <= TimeSpan.Zero)
            _opt.MaxMessageAge = TimeSpan.FromMinutes(10);
    }

    /// <summary>
    /// Verifierar webhook enligt canonical "<timestamp>\n<nonce>\n<body>".
    /// Accepterar header-alias: X-Swish-* samt X-*.
    /// Timestamp: unix sek/millis ELLER ISO-8601.
    /// Signatur: Base64 ELLER hex (SHA-256).
    /// Replay-skydd: bara om nonce skickas (då lagras och spärras återanvändning).
    /// </summary>
    public VerifyResult Verify(string body, IReadOnlyDictionary<string, string> headers, DateTimeOffset nowUtc)
    {
        if (headers is null) return VerifyResult.Fail("saknar headers");
        body ??= string.Empty;

        // 1) Hämta headers med alias-stöd (options-namn först, sedan kända alias)
        var sigHeader = GetHeader(headers,
            _opt.SignatureHeaderName,
            "X-Swish-Signature", "X-Signature");
        var tsHeader = GetHeader(headers,
            _opt.TimestampHeaderName,
            "X-Swish-Timestamp", "X-Timestamp");
        var nonce = GetHeader(headers,
            _opt.NonceHeaderName,
            "X-Swish-Nonce", "X-Nonce") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(sigHeader) || string.IsNullOrWhiteSpace(tsHeader))
            return VerifyResult.Fail("saknar signaturheader");

        // 2) Timestamp-parsning (unix sek/ms eller ISO-8601)
        if (!TryParseTimestamp(tsHeader!, out var ts))
            return VerifyResult.Fail("ogiltig timestamp");

        // 3) Klockskew + ålder
        var skew = (nowUtc - ts).Duration();
        if (skew > _opt.AllowedClockSkew)
            return VerifyResult.Fail("timestamp-skew");

        if (nowUtc - ts > _opt.MaxMessageAge)
            return VerifyResult.Fail("message-too-old");

        // 4) Canonical & HMAC
        //    OBS: canonical använder exakt tsHeader-strängen (inte normaliserad ts) + nonce + body
        var canonical = $"{tsHeader}\n{nonce}\n{body}";
        var key = Encoding.UTF8.GetBytes(_opt.SharedSecret);
        using var hmac = new HMACSHA256(key);
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));

        if (!TryDecodeSignature(sigHeader!, out var provided))
            return VerifyResult.Fail("invalid signature encoding");

        if (!FixedTimeEquals(expected, provided))
            return VerifyResult.Fail("signature mismatch");

        // 5) Replay-skydd – endast om klienten skickar ett nonce
        if (!string.IsNullOrEmpty(nonce))
        {
            var expires = nowUtc.Add(_opt.MaxMessageAge);
            if (!_nonceStore.TryRemember(nonce!, expires))
                return VerifyResult.Fail("replay upptäckt (nonce sedd tidigare)");
        }

        return VerifyResult.Ok();
    }

    // ----------------- Helpers -----------------

    private static string? GetHeader(IReadOnlyDictionary<string, string> headers, string? prefer, params string[] aliases)
    {
        // 1) Försök med det konfigurerade namnet
        if (!string.IsNullOrWhiteSpace(prefer) &&
            TryGet(headers, prefer!, out var v1) &&
            !string.IsNullOrWhiteSpace(v1))
            return v1;

        // 2) Försök med alias
        foreach (var a in aliases)
        {
            if (TryGet(headers, a, out var v2) && !string.IsNullOrWhiteSpace(v2))
                return v2;
        }
        return null;
    }

    private static bool TryGet(IReadOnlyDictionary<string, string> headers, string name, out string value)
    {
        foreach (var kv in headers)
        {
            if (kv.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }
        value = "";
        return false;
    }

    private static bool TryParseTimestamp(string input, out DateTimeOffset ts)
    {
        // Heltal? (sek/millis)
        if (long.TryParse(input, out var num))
        {
            // Heuristik: ≥13 tecken => millisekunder
            if (input.Length >= 13)
            {
                ts = DateTimeOffset.FromUnixTimeMilliseconds(num);
                return true;
            }
            ts = DateTimeOffset.FromUnixTimeSeconds(num);
            return true;
        }

        // ISO-8601
        if (DateTimeOffset.TryParse(
                input,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            ts = parsed.ToUniversalTime();
            return true;
        }

        ts = default;
        return false;
    }

    private static bool TryDecodeSignature(string sig, out byte[] bytes)
    {
        // Försök Base64
        try
        {
            bytes = Convert.FromBase64String(sig);
            return true;
        }
        catch { /* fall through */ }

        // Försök hex (64 tecken för SHA-256)
        if (sig.Length == 64)
        {
            try
            {
                bytes = Convert.FromHexString(sig);
                return true;
            }
            catch { /* ignore */ }
        }

        bytes = Array.Empty<byte>();
        return false;
    }

    private static bool FixedTimeEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }

    public readonly record struct VerifyResult(bool Success, string? Reason)
    {
        public static VerifyResult Ok() => new(true, null);
        public static VerifyResult Fail(string reason) => new(false, reason);
    }
}

