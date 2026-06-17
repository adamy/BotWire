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

namespace BotWire.Core.Guard;

/// <summary>
/// Atomic counter backend for rate-limiting dimensions that benefit from cross-instance accuracy.
/// The default in-process implementation lives in <see cref="RateLimiter"/>; a Redis
/// implementation is provided by <c>BotWire.Redis</c> for multi-container deployments.
/// </summary>
public interface IRateLimitStore
{
    /// <summary>
    /// Atomically increments <paramref name="key"/> by <paramref name="amount"/> and returns the
    /// new total. Sets <paramref name="expiry"/> on the first write; subsequent writes leave the
    /// existing TTL unchanged (fixed-window behaviour).
    /// </summary>
    Task<long> IncrementAsync(string key, long amount, TimeSpan expiry, CancellationToken ct = default);

    /// <summary>Returns the current counter value for <paramref name="key"/>, or zero if absent.</summary>
    Task<long> GetAsync(string key, CancellationToken ct = default);
}
