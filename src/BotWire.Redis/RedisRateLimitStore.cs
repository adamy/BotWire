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

using BotWire.Core.Guard;
using StackExchange.Redis;

namespace BotWire.Redis;

/// <summary>
/// Redis-backed <see cref="IRateLimitStore"/>. Uses atomic Lua for INCRBY + conditional EXPIRE so
/// the TTL is set exactly once on key creation without a read-modify-write race. Multiple app
/// instances sharing one Redis instance will enforce a single shared counter per key.
/// </summary>
internal sealed class RedisRateLimitStore : IRateLimitStore
{
    // Atomically increment KEYS[1] by ARGV[1]; set EXPIRE only if the key has no TTL yet
    // (TTL == -1 means key exists with no expiry; TTL == -2 means key does not exist).
    // On first write the INCRBY creates the key and TTL is -1, so EXPIRE is set.
    // On subsequent writes EXPIRE already exists (TTL >= 0), so it is not reset.
    private const string IncrScript = """
        local n = redis.call('INCRBY', KEYS[1], ARGV[1])
        if redis.call('TTL', KEYS[1]) < 0 then
            redis.call('EXPIRE', KEYS[1], ARGV[2])
        end
        return n
        """;

    private readonly IConnectionMultiplexer _mux;

    public RedisRateLimitStore(IConnectionMultiplexer mux) => _mux = mux;

    public async Task<long> IncrementAsync(string key, long amount, TimeSpan expiry, CancellationToken ct = default)
    {
        // Clamp to at least 1s: EXPIRE with a 0 (or negative) seconds argument deletes the key in
        // Redis, which would discard the increment we just applied. Sub-second TTLs arise at the
        // day boundary for the daily-token-budget key (ttl = midnight - now).
        var seconds = Math.Max(1, (long)expiry.TotalSeconds);
        var db = _mux.GetDatabase();
        var result = await db.ScriptEvaluateAsync(
            IncrScript,
            keys: [(RedisKey)key],
            values: [(RedisValue)amount, (RedisValue)seconds]).ConfigureAwait(false);
        return (long)result;
    }

    public async Task<long> GetAsync(string key, CancellationToken ct = default)
    {
        var db = _mux.GetDatabase();
        var value = await db.StringGetAsync(key).ConfigureAwait(false);
        return value.HasValue && long.TryParse((string?)value, out var n) ? n : 0;
    }
}
