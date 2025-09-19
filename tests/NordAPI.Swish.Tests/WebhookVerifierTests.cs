using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using NordAPI.Swish.Security.Webhooks;
using Xunit;

namespace NordAPI.Swish.Tests
{
    public class WebhookVerifierTests
    {
        private const string Secret = "dev_secret";

        private SwishWebhookVerifier CreateVerifier()
        {
            return new SwishWebhookVerifier(
                new SwishWebhookVerifierOptions
                {
                    SharedSecret = Secret,
                    AllowedClockSkew = TimeSpan.FromMinutes(5),
                    MaxMessageAge = TimeSpan.FromMinutes(10)
                },
                new InMemoryNonceStore()
            );
        }

        [Fact]
        public void Verify_ShouldPass_WhenValidSignature()
        {
            var body = "{\"id\":\"abc123\",\"amount\":100}";
            var ts = DateTimeOffset.UtcNow;

            var headers = TestHelper.MakeHeaders(Secret, body, ts);
            var verifier = CreateVerifier();

            var result = verifier.Verify(body, headers, ts);

            result.Success.Should().BeTrue(result.Reason);
        }

        [Fact]
        public void Verify_ShouldFail_WhenTimestampTooOld()
        {
            var body = "{\"id\":\"abc123\",\"amount\":100}";
            var tsOld = DateTimeOffset.UtcNow.AddMinutes(-15);

            var headers = TestHelper.MakeHeaders(Secret, body, tsOld);
            var verifier = CreateVerifier();

            var result = verifier.Verify(body, headers, DateTimeOffset.UtcNow);

            result.Success.Should().BeFalse();
            result.Reason.Should().NotBeNull();
            result.Reason!.ToLowerInvariant().Should().ContainAny("timestamp", "skew", "old");
        }

        [Fact]
        public void Verify_ShouldFail_OnReplay_WhenSameNonceReused()
        {
            var body = "{\"id\":\"abc123\",\"amount\":100}";
            var ts = DateTimeOffset.UtcNow;
            var nonce = Guid.NewGuid().ToString("N");

            var headers1 = TestHelper.MakeHeaders(Secret, body, ts, nonce);
            var headers2 = TestHelper.MakeHeaders(Secret, body, ts, nonce);

            var verifier = CreateVerifier();

            var first = verifier.Verify(body, headers1, ts);
            var second = verifier.Verify(body, headers2, ts);

            first.Success.Should().BeTrue(first.Reason);
            second.Success.Should().BeFalse();
            second.Reason.Should().NotBeNull();
            second.Reason!.ToLowerInvariant().Should().ContainAny("replay", "nonce");
        }
    }

    internal static class TestHelper
    {
        public static Dictionary<string, string> MakeHeaders(string secret, string body, DateTimeOffset ts, string? nonce = null)
        {
            var tsStr = ts.ToUniversalTime().ToString("o"); // ISO 8601-format
            var finalNonce = nonce ?? Guid.NewGuid().ToString("N");
            var message = $"{tsStr}\n{finalNonce}\n{body}";

            var key = Encoding.UTF8.GetBytes(secret);
            using var hmac = new System.Security.Cryptography.HMACSHA256(key);
            var sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(message)));

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Swish-Timestamp"] = tsStr,
                ["X-Swish-Nonce"]     = finalNonce,
                ["X-Swish-Signature"] = sig,
                ["X-Timestamp"]       = tsStr,
                ["X-Nonce"]           = finalNonce,
                ["X-Signature"]       = sig
            };
        }
    }
}
