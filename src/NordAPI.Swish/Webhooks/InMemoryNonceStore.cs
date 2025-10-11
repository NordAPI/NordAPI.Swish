using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NordAPI.Swish.Webhooks;

/// <summary>
/// Default in-memory implementation of <see cref="ISwishNonceStore"/>.
/// Used for local development or single-instance deployments.
/// </summary>
/// <remarks>
/// Nonces are kept in memory until their expiration time.
/// This implementation is not distributed and will reset on application restart.
/// </remarks>
public sealed class InMemoryNonceStore : ISwishNonceStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _nonces = new();

    /// <summary>
    /// Creates a new in-memory nonce store.
    /// </summary>
    public InMemoryNonceStore()
    {
    }

    /// <summary>
    /// Compatibility constructor. Accepts an unused parameter for backward compatibility with samples.
    /// </summary>
    /// <param name="_">Ignored parameter.</param>
    public InMemoryNonceStore(object? _)
    {
    }

    /// <inheritdoc />
    public Task<bool> TryRememberAsync(string nonce, DateTimeOffset expiresAtUtc, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Clean up expired entries opportunistically
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _nonces)
        {
            if (kvp.Value <= now)
                _nonces.TryRemove(kvp.Key, out _);
        }

        // Try to add the nonce if not seen or not expired
        if (expiresAtUtc <= now)
            return Task.FromResult(false);

        var added = _nonces.TryAdd(nonce, expiresAtUtc);
        return Task.FromResult(added);
    }
}

