using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;

namespace NordAPI.Swish.Webhooks;

/// <summary>
/// Endpoint filter that validates incoming Swish webhook requests
/// using <see cref="SwishWebhookVerifier"/> before invoking the handler.
/// </summary>
public sealed class SwishWebhookEndpointFilter : IEndpointFilter
{
    private readonly SwishWebhookVerifier _verifier;

    /// <summary>
    /// Creates a new instance of <see cref="SwishWebhookEndpointFilter"/>.
    /// </summary>
    /// <param name="verifier">The webhook verifier to use.</param>
    public SwishWebhookEndpointFilter(SwishWebhookVerifier verifier)
        => _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));

    /// <summary>
    /// Validates the incoming webhook request and invokes the next delegate
    /// only if verification succeeds.
    /// </summary>
    /// <param name="context">The endpoint invocation context.</param>
    /// <param name="next">The next endpoint delegate.</param>
    /// <returns>The result of the endpoint execution.</returns>
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var req = context.HttpContext.Request;

        // Read raw body
        req.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(
            req.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true))
        {
            rawBody = (await reader.ReadToEndAsync()) ?? string.Empty;
        }
        req.Body.Position = 0;

        // Pick headers (aliases are supported in the verifier)
        string Get(string name)
            => req.Headers.TryGetValue(name, out var v) ? v.ToString() : string.Empty;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Swish-Timestamp"] =
                Get("X-Swish-Timestamp").Length == 0 ? Get("X-Timestamp") : Get("X-Swish-Timestamp"),

            ["X-Swish-Signature"] =
                Get("X-Swish-Signature").Length == 0 ? Get("X-Signature") : Get("X-Swish-Signature"),

            ["X-Swish-Nonce"] =
                Get("X-Swish-Nonce").Length == 0 ? Get("X-Nonce") : Get("X-Swish-Nonce"),
        };

        var now = DateTimeOffset.UtcNow;
        var result = _verifier.Verify(rawBody, headers, now);

        if (!result.Success)
        {
            return Results.Json(
                new { reason = result.Reason ?? "sig-or-replay-failed" },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // Mark as verified if the consumer wants to read it in the handler
        context.HttpContext.Items["SwishWebhookVerified"] = true;

        return await next(context);
    }
}
