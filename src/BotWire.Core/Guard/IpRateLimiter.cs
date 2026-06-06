// BotWire
// Copyright (C) 2026  Object IT Limited
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace BotWire.Core.Guard;

/// <summary>
/// Per-IP sliding-window rate limiter. Stale IP entries are pruned lazily on the next
/// request from the same IP — no background timer needed.
/// Thread-safe: each per-IP queue is protected by a lock on that queue instance.
/// </summary>
public sealed class IpRateLimiter
{
    private readonly int _maxRequests;
    private readonly Func<DateTimeOffset> _clock;
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _windows = new();

    /// <summary>Initializes the rate limiter using the system clock.</summary>
    public IpRateLimiter(IOptions<RateLimiterOptions> options)
        : this(options, () => DateTimeOffset.UtcNow) { }

    /// <summary>Initializes the rate limiter with an injectable clock (for testing).</summary>
    internal IpRateLimiter(IOptions<RateLimiterOptions> options, Func<DateTimeOffset> clock)
    {
        _maxRequests = options.Value.MaxRequestsPerIpPerMinute;
        _clock = clock;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="ip"/> is within its request quota for the current window;
    /// <see langword="false"/> when the quota is exceeded.
    /// </summary>
    /// <param name="ip">The client IP address string (IPv4 or IPv6).</param>
    public bool IsAllowed(string ip)
    {
        var window = _windows.GetOrAdd(ip, _ => new Queue<DateTimeOffset>());
        var now = _clock();
        var cutoff = now.AddMinutes(-1);

        lock (window)
        {
            while (window.Count > 0 && window.Peek() < cutoff)
                window.Dequeue();

            if (window.Count >= _maxRequests)
                return false;

            window.Enqueue(now);
            return true;
        }
    }
}
