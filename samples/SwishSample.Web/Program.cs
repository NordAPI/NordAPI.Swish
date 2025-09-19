using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using NordAPI.Swish;
using NordAPI.Swish.DependencyInjection;
using NordAPI.Swish.Security.Webhooks;

var builder = WebApplication.CreateBuilder(args);

// 1) Swish SDK-klient i DI
builder.Services.AddSwishClient(opts =>
{
    opts.BaseAddress = new Uri(
        Environment.GetEnvironmentVariable("SWISH_BASE_URL")
        ?? "https://example.invalid");
    opts.ApiKey = Environment.GetEnvironmentVariable("SWISH_API_KEY")
                  ?? "dev-key";
    opts.Secret = Environment.GetEnvironmentVariable("SWISH_SECRET")
                  ?? "dev-secret";
});

// 2) Replay-skydd (nonce-store)
builder.Services.AddSingleton<ISwishNonceStore, InMemoryNonceStore>();

// 3) Webhook-verifierare (läser SWISH_WEBHOOK_SECRET)
builder.Services.AddSingleton(sp =>
{
    var cfg    = sp.GetRequiredService<IConfiguration>();
    var secret = Environment.GetEnvironmentVariable("SWISH_WEBHOOK_SECRET")
                 ?? cfg["SWISH_WEBHOOK_SECRET"];
    if (string.IsNullOrWhiteSpace(secret))
        throw new InvalidOperationException("Missing SWISH_WEBHOOK_SECRET.");

    var nonces = sp.GetRequiredService<ISwishNonceStore>();
    var opts = new SwishWebhookVerifierOptions
    {
        SharedSecret = secret
    };
    return new SwishWebhookVerifier(opts, nonces);
});

var app = builder.Build();

// Bas-endpoints
app.MapGet("/", () =>
    "Swish sample is running. Try /health, /di-check, /ping, or POST /webhook/swish").AllowAnonymous();
app.MapGet("/health", () => "ok").AllowAnonymous();
app.MapGet("/di-check", (ISwishClient swish) =>
    swish is not null ? "ISwishClient is registered" : "not found").AllowAnonymous();
app.MapGet("/ping", () => Results.Ok("pong (mocked)")).AllowAnonymous();

// 📬 Webhook: signatur + tidsfönster + replay-skydd + debugläge
app.MapPost("/webhook/swish", async (
    HttpRequest req,
    [FromServices] SwishWebhookVerifier verifier) =>
{
    var isDebug  = string.Equals(
        Environment.GetEnvironmentVariable("SWISH_DEBUG"), "1", StringComparison.Ordinal);
    var allowOld = string.Equals(
        Environment.GetEnvironmentVariable("SWISH_ALLOW_OLD_TS"), "1", StringComparison.Ordinal);

    // Tillåt buffering
    req.EnableBuffering();

    // 1) Läs body (garanterat icke-null)
    string body;
    using (var reader = new StreamReader(req.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        body = (await reader.ReadToEndAsync()) ?? string.Empty;
    req.Body.Position = 0;

    // 2) Logga alla headers (säker string.Join)
    if (isDebug)
    {
        Console.WriteLine("[DEBUG] Inkommande headers:");
        foreach (var h in req.Headers)
        {
            var values = h.Value.ToArray();
            Console.WriteLine($"  {h.Key} = {string.Join(", ", values)}");
        }
    }

    // 3) Plocka ut huvudvärden (med säkra fallback till tom sträng)
    var tsHeader  = (req.Headers["X-Swish-Timestamp"].ToString()
                  ?? req.Headers["X-Timestamp"].ToString()) ?? string.Empty;

    var sigHeader = (req.Headers["X-Swish-Signature"].ToString()
                  ?? req.Headers["X-Signature"].ToString()) ?? string.Empty;

    var nonce     = req.Headers["X-Swish-Nonce"].ToString();
    if (string.IsNullOrWhiteSpace(nonce))
        nonce = req.Headers["X-Nonce"].ToString();
    var finalNonce = nonce ?? string.Empty;

    // Debug: exakt timestamp-sträng
    if (isDebug) Console.WriteLine($"[DEBUG] Raw tsHeader: '{tsHeader}'");

    if (string.IsNullOrWhiteSpace(tsHeader) ||
        string.IsNullOrWhiteSpace(sigHeader))
    {
        var payload = new { reason = "missing-headers", tsHeader, sigHeader };
        return isDebug
            ? Results.BadRequest(payload)
            : Results.BadRequest("Missing X-Swish-Timestamp or X-Signature");
    }

    // 4) Timestamp-parse (sek/millis/ISO-8601)
    if (!TryParseTimestamp(tsHeader, out var ts))
    {
        var payload = new { reason = "bad-timestamp", tsHeader };
        return isDebug
            ? Results.BadRequest(payload)
            : Results.BadRequest("Invalid X-Swish-Timestamp");
    }

    // 5) Skew/age
    var now = DateTimeOffset.UtcNow;
    var skewSeconds = Math.Abs((now - ts).TotalSeconds);

    if (!allowOld && skewSeconds > TimeSpan.FromMinutes(5).TotalSeconds)
    {
        var payload = new
        {
            reason = "timestamp-skew",
            now    = now.ToUnixTimeSeconds(),
            ts     = ts.ToUnixTimeSeconds(),
            deltaSeconds = (int)(now - ts).TotalSeconds
        };
        return isDebug
            ? Results.Json(payload, statusCode: 401)
            : Results.Unauthorized();
    }

    // 6) Logga canonical för felsökning
    if (isDebug)
    {
        Console.WriteLine("[DEBUG] Server-canonical:");
        Console.WriteLine($"{tsHeader}\n{finalNonce}\n{body}");
    }

    // 7) Verifiera signatur + anti-replay
    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["X-Swish-Timestamp"] = tsHeader,
        ["X-Swish-Signature"] = sigHeader,
        ["X-Swish-Nonce"]     = finalNonce,
        ["X-Timestamp"]       = tsHeader,
        ["X-Signature"]       = sigHeader,
        ["X-Nonce"]           = finalNonce
    };

    var result = verifier.Verify(body, headers, DateTimeOffset.UtcNow);
    if (!result.Success)
    {
        var payload = new { reason = result.Reason ?? "sig-or-replay-failed" };
        return isDebug
            ? Results.Json(payload, statusCode: 401)
            : Results.Unauthorized();
    }

    return Results.Ok(new { received = true });
}).AllowAnonymous();

app.Run();


/// <summary>
/// Accepterar Unix sekunder/millis och ISO-8601 (UTC).
/// </summary>
static bool TryParseTimestamp(string tsHeader, out DateTimeOffset ts)
{
  // Heltal? → sekunder/millis
  if (long.TryParse(tsHeader, out var num))
  {
    if (tsHeader.Length >= 13)
    {
      ts = DateTimeOffset.FromUnixTimeMilliseconds(num).ToUniversalTime();
      return true;
    }
    ts = DateTimeOffset.FromUnixTimeSeconds(num).ToUniversalTime();
    return true;
  }

  // ISO-8601
  if (DateTimeOffset.TryParse(
          tsHeader,
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
public partial class Program { }

