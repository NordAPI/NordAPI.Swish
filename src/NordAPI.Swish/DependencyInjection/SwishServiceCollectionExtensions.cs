using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using NordAPI.Swish.Security.Http;
using Polly;
using Polly.Extensions.Http;

namespace NordAPI.Swish.DependencyInjection;

public static class SwishServiceCollectionExtensions
{
    /// <summary>
    /// Registers ISwishClient using HttpClientFactory.
    ///
    /// Pipeline (inner → outer):
    ///   RateLimitingHandler → HmacSigningHandler → …(any test/host handlers) → Polly retry (outermost)
    ///
    /// Primary handler:
    ///   Assigned by a preserving IHttpMessageHandlerBuilderFilter that only sets a Primary
    ///   if tests/host did not already provide one. mTLS via explicit cert or env; plain fallback.
    /// </summary>
    public static IServiceCollection AddSwishClient(
        this IServiceCollection services,
        Action<SwishOptions> configure,
        X509Certificate2? clientCertificate = null)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var opts = new SwishOptions();
        configure(opts);

        if (opts.BaseAddress is null)
            throw new InvalidOperationException("SwishOptions.BaseAddress must be set.");
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
            throw new InvalidOperationException("SwishOptions.ApiKey must be set.");
        if (string.IsNullOrWhiteSpace(opts.Secret))
            throw new InvalidOperationException("SwishOptions.Secret must be set.");

        services.AddSingleton(opts);

        // OUTERMOST retry policy.
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(new[]
            {
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(200),
                TimeSpan.FromMilliseconds(400),
            });

        // Primary factory: explicit cert wins; else env/fallback.
        Func<HttpMessageHandler> primaryFactory = () =>
        {
            if (clientCertificate is not null)
            {
                var h = new SocketsHttpHandler();
                h.SslOptions.ClientCertificates = new X509CertificateCollection { clientCertificate };
#if DEBUG
                // DEV ONLY: relaxed server validation in Debug; NEVER in Release.
                h.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
#endif
                return h;
            }

            // Env-controlled (SWISH_PFX_PATH + SWISH_PFX_PASS) or plain if missing.
            return SwishMtlsHandlerFactory.Create();
        };

        // Ensure the Primary-preserving filter is registered once.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter>(
                new SwishPrimaryPreservingFilter(primaryFactory)));

        services
            .AddHttpClient<ISwishClient, SwishClient>("Swish", (_, client) =>
            {
                client.BaseAddress = opts.BaseAddress!;
            })
            // Inner handlers (built inner → outer; retry added last = outermost)
            .AddHttpMessageHandler(() =>
                new RateLimitingHandler(
                    maxConcurrency: 4,
                    minDelayBetweenCalls: TimeSpan.FromMilliseconds(100)))
            .AddHttpMessageHandler(() =>
                new HmacSigningHandler(opts.ApiKey!, opts.Secret!))
            // OUTERMOST: retry
            .AddPolicyHandler(retryPolicy);

        return services;
    }
}











