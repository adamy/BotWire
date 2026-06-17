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

namespace BotWire.Redis.Tests;

public sealed class RedisRateLimitStoreTests : IDisposable
{
    private readonly IConnectionMultiplexer _mux;
    private readonly IRateLimitStore _store;
    private readonly List<string> _keys = [];

    public RedisRateLimitStoreTests()
    {
        _mux = ConnectionMultiplexer.Connect(SkipIfNoRedisFact.ConnectionString);
        _store = new RedisRateLimitStore(_mux);
    }

    public void Dispose()
    {
        if (_keys.Count > 0)
        {
            var db = _mux.GetDatabase();
            db.KeyDelete([.. _keys.Select(k => (RedisKey)k)]);
        }
        _mux.Dispose();
    }

    private string Key(string suffix)
    {
        var k = $"botwire:rl:test:{Guid.NewGuid():N}:{suffix}";
        _keys.Add(k);
        return k;
    }

    // ── GetAsync ─────────────────────────────────────────────────────────────────

    [SkipIfNoRedisFact]
    public async Task GetAsync_MissingKey_ReturnsZero()
    {
        var result = await _store.GetAsync(Key("missing"));
        Assert.Equal(0, result);
    }

    [SkipIfNoRedisFact]
    public async Task GetAsync_ExistingKey_ReturnsCount()
    {
        var key = Key("get");
        await _store.IncrementAsync(key, 5, TimeSpan.FromMinutes(1));

        var result = await _store.GetAsync(key);

        Assert.Equal(5, result);
    }

    // ── IncrementAsync ───────────────────────────────────────────────────────────

    [SkipIfNoRedisFact]
    public async Task IncrementAsync_FirstCall_ReturnsAmountAndSetsTtl()
    {
        var key = Key("firstincr");
        var result = await _store.IncrementAsync(key, 1, TimeSpan.FromSeconds(60));

        Assert.Equal(1, result);

        var db = _mux.GetDatabase();
        var ttl = await db.KeyTimeToLiveAsync(key);
        Assert.NotNull(ttl);
        Assert.True(ttl!.Value.TotalSeconds > 0);
    }

    [SkipIfNoRedisFact]
    public async Task IncrementAsync_SubsequentCalls_Accumulates()
    {
        var key = Key("accum");
        await _store.IncrementAsync(key, 1, TimeSpan.FromSeconds(60));
        await _store.IncrementAsync(key, 1, TimeSpan.FromSeconds(60));
        var result = await _store.IncrementAsync(key, 1, TimeSpan.FromSeconds(60));

        Assert.Equal(3, result);
    }

    [SkipIfNoRedisFact]
    public async Task IncrementAsync_SubsequentCalls_DoNotResetTtl()
    {
        var key = Key("ttlstable");
        await _store.IncrementAsync(key, 1, TimeSpan.FromSeconds(60));

        var db = _mux.GetDatabase();
        var ttlFirst = await db.KeyTimeToLiveAsync(key);

        await Task.Delay(200);
        await _store.IncrementAsync(key, 1, TimeSpan.FromSeconds(60));
        var ttlSecond = await db.KeyTimeToLiveAsync(key);

        // TTL should not be reset — second call should show slightly less remaining time.
        Assert.NotNull(ttlFirst);
        Assert.NotNull(ttlSecond);
        Assert.True(ttlSecond!.Value <= ttlFirst!.Value);
    }

    [SkipIfNoRedisFact]
    public async Task IncrementAsync_ByAmount_IncrementsCorrectly()
    {
        var key = Key("byamount");
        await _store.IncrementAsync(key, 100, TimeSpan.FromSeconds(60));
        var result = await _store.IncrementAsync(key, 50, TimeSpan.FromSeconds(60));

        Assert.Equal(150, result);
    }

    [SkipIfNoRedisFact]
    public async Task IncrementAsync_SubSecondExpiry_DoesNotDeleteKey()
    {
        // A sub-second TTL (truncates to 0s) must not trigger EXPIRE 0, which would delete the
        // key and discard the increment. The store clamps to >= 1s.
        var key = Key("subsecond");
        var result = await _store.IncrementAsync(key, 7, TimeSpan.FromMilliseconds(400));

        Assert.Equal(7, result);
        Assert.Equal(7, await _store.GetAsync(key)); // survived

        var db = _mux.GetDatabase();
        var ttl = await db.KeyTimeToLiveAsync(key);
        Assert.NotNull(ttl); // has an expiry, not deleted and not persistent
        Assert.True(ttl!.Value.TotalSeconds > 0);
    }

    // ── cross-instance (Task 34 AC 4) ────────────────────────────────────────────

    [SkipIfNoRedisFact]
    public async Task TwoStores_SameRedis_ShareCounter()
    {
        var key = Key("crossinstance");
        var storeA = new RedisRateLimitStore(_mux);
        var storeB = new RedisRateLimitStore(_mux);

        await storeA.IncrementAsync(key, 1, TimeSpan.FromMinutes(1));
        await storeB.IncrementAsync(key, 1, TimeSpan.FromMinutes(1));
        var total = await storeA.GetAsync(key);

        // Both increments land on the same key — total is 2, not 1.
        Assert.Equal(2, total);
    }

    // ── daily token budget key (Task 34 AC 5) ────────────────────────────────────

    [SkipIfNoRedisFact]
    public async Task DailyBudgetKey_ExpiresAtMidnight()
    {
        var today = DateTime.UtcNow.Date;
        var midnight = today.AddDays(1);
        var ttl = midnight - DateTime.UtcNow;

        var key = Key($"tokens:{today:yyyyMMdd}");
        await _store.IncrementAsync(key, 1000, ttl);

        var db = _mux.GetDatabase();
        var keyTtl = await db.KeyTimeToLiveAsync(key);

        Assert.NotNull(keyTtl);
        // TTL should be within a few seconds of seconds-until-midnight.
        Assert.True(Math.Abs((keyTtl!.Value - ttl).TotalSeconds) < 5);
    }
}
