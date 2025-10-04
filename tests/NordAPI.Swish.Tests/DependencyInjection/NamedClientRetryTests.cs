using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using NordAPI.Swish.DependencyInjection;

namespace NordAPI.Swish.Tests.DependencyInjection
{
    /// <summary>
    /// Verifies that the named client "Swish" retries on transient errors.
    /// The test injects a sequence handler that returns 500 first, then 200.
    /// Expected: final result is 200 (OK) and at least 2 attempts are made.
    /// </summary>
    public class NamedClientRetryTests
    {
        [Fact]
        public async Task SwishClient_Retries_On_Transient_5xx_Then_Succeeds()
        {
            // Ensure mTLS is disabled in test so pipeline builds without certs.
            Environment.SetEnvironmentVariable("SWISH_PFX_PATH", null);
            Environment.SetEnvironmentVariable("SWISH_PFX_BASE64", null);
            Environment.SetEnvironmentVariable("SWISH_PFX_PASSWORD", null);
            Environment.SetEnvironmentVariable("SWISH_PFX_PASS", null);

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddDebug().AddConsole());

            // Register transport pipeline (includes Polly retry outermost).
            services.AddSwishMtlsTransport();

            // Sequence: first call -> 500, second call -> 200.
            var seq = new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("boom")
                },
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("ok")
                }
            );

            // Important: set SequenceHandler as PRIMARY, so Polly (delegating) wraps it.
            services.AddHttpClient("Swish")
                    .ConfigurePrimaryHttpMessageHandler(_ => seq);

            using var sp = services.BuildServiceProvider();
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("Swish");

            var res = await client.GetAsync("http://unit.test/ping");

            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.True(seq.Attempts >= 2, $"Expected at least 2 attempts, got {seq.Attempts}");
        }

        /// <summary>
        /// A delegating handler that returns a predefined sequence of responses.
        /// After the sequence is exhausted, the last response is reused.
        /// </summary>
        private sealed class SequenceHandler : DelegatingHandler
        {
            private readonly HttpResponseMessage[] _responses;
            private int _index = -1;

            public int Attempts => Math.Max(0, _index + 1);

            public SequenceHandler(params HttpResponseMessage[] responses)
            {
                if (responses is null || responses.Length == 0)
                    throw new ArgumentException("At least one response is required.", nameof(responses));

                _responses = responses;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var next = Interlocked.Increment(ref _index);
                var i = next < _responses.Length ? next : _responses.Length - 1;
                return Task.FromResult(CloneIfConsumed(_responses[i]));
            }

            private static HttpResponseMessage CloneIfConsumed(HttpResponseMessage original)
            {
                // Clone status + simple text body (reuse of HttpResponseMessage is unsafe).
                var text = original.Content is null ? "" : original.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var clone = new HttpResponseMessage(original.StatusCode)
                {
                    ReasonPhrase = original.ReasonPhrase,
                    Content = new StringContent(text)
                };
                foreach (var header in original.Headers)
                    clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
                return clone;
            }
        }
    }
}



