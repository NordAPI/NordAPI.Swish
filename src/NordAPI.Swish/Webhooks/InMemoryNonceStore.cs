﻿using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NordAPI.Swish.Webhooks;

/// <summary>
/// In-memory implementation of <see cref="ISwishNonceStore"/> used for local development and testing.
/// Thread-safe, but not suitable for multi-instance or production environments.
/// </summary>
public sealed class InMemoryNonceStore : ISwishNonceStore, IDisposable
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _entries = new();
    private readonly Timer _cleanupTimer;

    // Ensures TryRemember is atomic under concurrency (check + add must be one operation).
    private readonly object _gate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryNonceStore"/> class.
    /// Starts a background cleanup task that periodically removes expired nonces.
    /// </summary>
    /// <param name="cleanupInterval">How often expired entries are cleaned up. Default: 1 minute.</param>
    public InMemoryNonceStore(TimeSpan? cleanupInterval = null)
    {
        var interval = cleanupInterval ?? TimeSpan.FromMinutes(1);
        _cleanupTimer = new Timer(_ => Cleanup(), null, interval, interval);
    }

    /// <summary>
    /// Tries to remember a nonce until the specified expiration time.
    /// Returns <c>true</c> if it was newly added, <c>false</c> if it already existed (replay).
    /// </summary>
    public Task<bool> TryRememberAsync(string nonce, DateTimeOffset expiresAtUtc, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nonce))
            return Task.FromResult(false);

        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;

            if (expiresAtUtc <= now)
                return Task.FromResult(false);


            if (_entries.TryGetValue(nonce, out var existing))
            {
                // Still valid -> replay
                if (existing > now)
                    return Task.FromResult(false);

                // Expired -> allow re-use by removing the old entry first
                _entries.TryRemove(nonce, out _);
            }

            _entries[nonce] = expiresAtUtc;
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Performs synchronous TryRemember call for compatibility.
    /// </summary>
    public bool TryRemember(string nonce, DateTimeOffset expiresAtUtc, CancellationToken ct = default)
        => TryRememberAsync(nonce, expiresAtUtc, ct).GetAwaiter().GetResult();

    private void Cleanup()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _entries)
        {
            if (kvp.Value <= now)
                _entries.TryRemove(kvp.Key, out _);
        }
    }

    /// <summary>
    /// Stops the background cleanup timer and releases resources.
    /// </summary>
    public void Dispose()
    {
        _cleanupTimer.Dispose();
        _entries.Clear();
    }
}

