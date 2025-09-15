using System;
using System.IO;
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
    "Swish sample is running. Try /health, /di-check, /ping, or POST /webhook/swish");
app.MapGet("/health", () => "ok");
app.MapGet("/di-check", (ISwishClient swish) => 
    swish is not null ? "ISwishClient is registered" : "not found");
app.MapGet("/ping", () => Results.Ok("pong (mocked)"));

// 📬 Webhook: signatur + tidsfönster + replay-skydd + debugläge
app.MapPost("/webhook/swish", async (
    HttpRequest req,
    [FromServices] SwishWebhookVerifier verifier) =>
{
    var isDebug  = string.Equals(
        Environment.GetEnvironmentVariable("SWISH_DEBUG"), "1");
    var allowOld = string.Equals(
        Environment.GetEnvironmentVariable("SWISH_ALLOW_OLD_TS"), "1");

    // 1) Läs body
    string body;
    using (var reader = new StreamReader(req.Body, Encoding.UTF8))
        body = await reader.ReadToEndAsync();

    // 2) Logga alla headers (för felsökning)
    Console.WriteLine("[DEBUG] Inkommande headers:");
    foreach (var h in req.Headers)
        Console.WriteLine($"  {h.Key} = {string.Join(", ", h.Value)}");

    // 3) Plocka ut huvudvärden
    var tsHeader   = req.Headers["X-Swish-Timestamp"].ToString();
    var sigHeader  = req.Headers["X-Swish-Signature"].ToString();
    var nonce      = req.Headers["X-Swish-Nonce"].ToString();
    if (string.IsNullOrWhiteSpace(nonce))
        nonce = req.Headers["X-Nonce"].ToString();
    var finalNonce = nonce ?? "";

    // ── Steg C Debug: logga exakt vad servern ser som tsHeader
    Console.WriteLine($"[DEBUG] Raw tsHeader: '{tsHeader}'");
    // ─────────────────────────────────────────────────

    if (string.IsNullOrWhiteSpace(tsHeader) ||
        string.IsNullOrWhiteSpace(sigHeader))
    {
        var payload = new { reason = "missing-headers", tsHeader, sigHeader };
        return isDebug
            ? Results.BadRequest(payload)
            : Results.BadRequest(
                  "Missing X-Swish-Timestamp or X-Swish-Signature");
    }

    // 4) Försök tolka tsHeader som long
    if (!long.TryParse(tsHeader, out var tsRaw))
    {
        var payload = new { reason = "bad-timestamp", tsHeader };
        return isDebug
            ? Results.BadRequest(payload)
            : Results.BadRequest("Invalid X-Swish-Timestamp");
    }

    // 5) Normalisera sekunder vs millisekunder
    long tsSeconds = tsRaw > 10_000_000_000
        ? tsRaw / 1000
        : tsRaw;

    // 6) Kontrollera tidsfönster ±5 min
    var nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    if (Math.Abs(nowSec - tsSeconds) >
            TimeSpan.FromMinutes(5).TotalSeconds
        && !allowOld)
    {
        var payload = new
        {
            reason       = "timestamp-skew",
            now          = nowSec,
            ts           = tsSeconds,
            deltaSeconds = nowSec - tsSeconds
        };
        return isDebug
            ? Results.Json(payload, statusCode: 401)
            : Results.Unauthorized();
    }

    // 7) Logga den canonical-sträng servern använder
    Console.WriteLine("[DEBUG] Server-canonical:");
    Console.WriteLine($"{tsHeader}\n{finalNonce}\n{body}");

    // 8) Verifiera signatur + replay
    var headers = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase)
    {
        ["X-Swish-Timestamp"] = tsHeader,
        ["X-Swish-Signature"] = sigHeader,
        ["X-Swish-Nonce"]     = finalNonce,
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
});

app.Run();
