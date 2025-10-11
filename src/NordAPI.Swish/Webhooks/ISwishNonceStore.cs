using System;
using System.Threading;
using System.Threading.Tasks;

namespace NordAPI.Swish.Webhooks;

/// <summary>
/// Contract for a replay-protection store used to remember nonces for a limited time.
/// Implementations must be thread-safe and suitable for concurrent request processing.
/// </summary>
public interface ISwishNonceStore
{
    /// <summary>
    /// Tries to remember the specified <paramref name="nonce"/> until <paramref name="expiresAtUtc"/>.
    /// Returns <c>true</c> if the nonce was stored (i.e., not seen before and not expired),
    /// or <c>false</c> if the nonce already existed (replay) or the expiry is in the past.
    /// </summary>
    /// <param name="nonce">The unique request nonce to store.</param>
    /// <param name="expiresAtUtc">Absolute UTC expiry timestamp after which the nonce is no longer considered.</param>
    /// <param name="ct">An optional cancellation token.</param>
    /// <returns>
    /// <c>true</c> if the nonce was newly stored; <c>false</c> if it already existed or was not stored due to expiry.
    /// </returns>
    Task<bool> TryRememberAsync(string nonce, DateTimeOffset expiresAtUtc, CancellationToken ct = default);
}
