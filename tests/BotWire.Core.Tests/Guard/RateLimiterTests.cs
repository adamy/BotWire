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
using Microsoft.Extensions.Options;

namespace BotWire.Core.Tests.Guard;

/// <summary>Unit tests for the five rate-limiting dimensions in <see cref="RateLimiter"/>.</summary>
public class RateLimiterTests
{
    private static RateLimiter Create(RateLimitOptions options, Func<DateTimeOffset>? clock = null)
    {
        var opts = Options.Create(options);
        return clock is null ? new RateLimiter(opts) : new RateLimiter(opts, clock);
    }

    // ── 1. MaxConcurrentSessions — queue, never reject ──────────────────────────

    [Fact]
    public async Task Concurrency_BlocksOverCap_ReleasesOnDispose()
    {
        using var limiter = Create(new RateLimitOptions { MaxConcurrentSessions = 1 });

        var first = await limiter.AcquireConcurrencySlotAsync();

        // Second acquire must not complete while the single slot is held.
        var second = limiter.AcquireConcurrencySlotAsync();
        Assert.False(second.IsCompleted);

        first.Dispose();

        var handle = await second; // now succeeds
        handle.Dispose();
    }

    [Fact]
    public async Task Concurrency_Disabled_AlwaysGrantsImmediately()
    {
        using var limiter = Create(new RateLimitOptions { MaxConcurrentSessions = 0 });

        var a = await limiter.AcquireConcurrencySlotAsync();
        var b = await limiter.AcquireConcurrencySlotAsync(); // would block if enabled at cap 0
        a.Dispose();
        b.Dispose();
    }

    [Fact]
    public async Task Concurrency_DoubleDispose_DoesNotOverRelease()
    {
        using var limiter = Create(new RateLimitOptions { MaxConcurrentSessions = 1 });

        var slot = await limiter.AcquireConcurrencySlotAsync();
        slot.Dispose();
        slot.Dispose(); // second dispose must be a no-op

        var next = limiter.AcquireConcurrencySlotAsync();
        Assert.True(next.IsCompleted); // exactly one slot free, not two
        (await next).Dispose();
    }

    // ── 2. MaxMessagesPerMinute — delay, never reject ───────────────────────────

    [Fact]
    public void PerMinute_WithinQuota_NoDelay()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var limiter = Create(new RateLimitOptions { MaxMessagesPerMinute = 3 }, clock.Now);

        Assert.Equal(TimeSpan.Zero, limiter.NextPerMinuteDelay("s1"));
        Assert.Equal(TimeSpan.Zero, limiter.NextPerMinuteDelay("s1"));
        Assert.Equal(TimeSpan.Zero, limiter.NextPerMinuteDelay("s1"));
    }

    [Fact]
    public void PerMinute_OverQuota_DelaysUntilOldestExpires()
    {
        var start = DateTimeOffset.UtcNow;
        var clock = new FakeClock(start);
        var limiter = Create(new RateLimitOptions { MaxMessagesPerMinute = 2 }, clock.Now);

        limiter.NextPerMinuteDelay("s1"); // t=0
        clock.Advance(TimeSpan.FromSeconds(20));
        limiter.NextPerMinuteDelay("s1"); // t=20 — quota now full

        // Third send at t=20: oldest (t=0) frees at t=60, so delay ≈ 40s.
        var delay = limiter.NextPerMinuteDelay("s1");
        Assert.Equal(TimeSpan.FromSeconds(40), delay);
    }

    [Fact]
    public void PerMinute_DifferentSessions_IndependentWindows()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var limiter = Create(new RateLimitOptions { MaxMessagesPerMinute = 1 }, clock.Now);

        Assert.Equal(TimeSpan.Zero, limiter.NextPerMinuteDelay("s1"));
        // s2 has its own window — still within quota.
        Assert.Equal(TimeSpan.Zero, limiter.NextPerMinuteDelay("s2"));
    }

    [Fact]
    public void PerMinute_Disabled_NeverDelays()
    {
        var limiter = Create(new RateLimitOptions { MaxMessagesPerMinute = 0 });
        for (var i = 0; i < 10; i++)
            Assert.Equal(TimeSpan.Zero, limiter.NextPerMinuteDelay("s1"));
    }

    // ── 3. MaxMessagesPerSession — prompt new conversation ──────────────────────

    [Theory]
    [InlineData(49, false)]
    [InlineData(50, true)]
    [InlineData(51, true)]
    public void SessionCap_TripsAtOrOverLimit(int count, bool expected)
    {
        var limiter = Create(new RateLimitOptions { MaxMessagesPerSession = 50 });
        Assert.Equal(expected, limiter.IsSessionOverMessageCap(count));
    }

    [Fact]
    public void SessionCap_Disabled_NeverTrips()
    {
        var limiter = Create(new RateLimitOptions { MaxMessagesPerSession = 0 });
        Assert.False(limiter.IsSessionOverMessageCap(10_000));
    }

    // ── 4. MaxSessionsPerIpPerHour — reject ─────────────────────────────────────

    [Fact]
    public void IpHour_AllowsUpToCapThenRejects()
    {
        var limiter = Create(new RateLimitOptions { MaxSessionsPerIpPerHour = 2 });
        Assert.True(limiter.TryRegisterIpSession("1.2.3.4"));
        Assert.True(limiter.TryRegisterIpSession("1.2.3.4"));
        Assert.False(limiter.TryRegisterIpSession("1.2.3.4")); // 3rd rejected
    }

    [Fact]
    public void IpHour_AllowsAgainAfterWindowExpires()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var limiter = Create(new RateLimitOptions { MaxSessionsPerIpPerHour = 1 }, clock.Now);

        Assert.True(limiter.TryRegisterIpSession("9.9.9.9"));
        Assert.False(limiter.TryRegisterIpSession("9.9.9.9"));

        clock.Advance(TimeSpan.FromMinutes(61));
        Assert.True(limiter.TryRegisterIpSession("9.9.9.9"));
    }

    [Fact]
    public void IpHour_Disabled_NeverRejects()
    {
        var limiter = Create(new RateLimitOptions { MaxSessionsPerIpPerHour = 0 });
        for (var i = 0; i < 100; i++)
            Assert.True(limiter.TryRegisterIpSession("1.2.3.4"));
    }

    // ── 5. DailyTokenBudget — degraded response ─────────────────────────────────

    [Fact]
    public void TokenBudget_ExhaustsAtBudgetThenResetsNextDay()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 6, 12, 10, 0, 0, TimeSpan.Zero));
        var limiter = Create(new RateLimitOptions { DailyTokenBudget = 100 }, clock.Now);

        Assert.False(limiter.IsTokenBudgetExhausted());
        limiter.AddTokens(60);
        Assert.False(limiter.IsTokenBudgetExhausted());
        limiter.AddTokens(40);
        Assert.True(limiter.IsTokenBudgetExhausted()); // 100 >= 100

        // Roll to the next UTC day — counter resets.
        clock.Advance(TimeSpan.FromDays(1));
        Assert.False(limiter.IsTokenBudgetExhausted());
        Assert.Equal(0, limiter.TokensUsedToday);
    }

    [Fact]
    public void TokenBudget_Disabled_NeverExhaustsAndIgnoresAdds()
    {
        var limiter = Create(new RateLimitOptions { DailyTokenBudget = 0 });
        limiter.AddTokens(10_000_000);
        Assert.False(limiter.IsTokenBudgetExhausted());
        Assert.Equal(0, limiter.TokensUsedToday);
    }

    // ── Distributed counter store (Redis path) ──────────────────────────────────

    [Fact]
    public async Task PerMinute_DistributedStore_WithinQuota_NoWaitAndIncrements()
    {
        var store = new FakeRateLimitStore();
        using var limiter = new RateLimiter(
            Options.Create(new RateLimitOptions { MaxMessagesPerMinute = 3 }), store);

        var delay = await limiter.DelayForPerMinuteAsync("s1");

        Assert.Equal(TimeSpan.Zero, delay);
        Assert.Equal(1, store.Counts["botwire:rl:msgmin:s1"]); // slot consumed
    }

    [Fact]
    public async Task PerMinute_DistributedStore_OverQuota_WaitsUntilWindowFreesThenAdmits()
    {
        var store = new FakeRateLimitStore();
        store.Counts["botwire:rl:msgmin:s1"] = 5; // window full (cap 5)
        using var limiter = new RateLimiter(
            Options.Create(new RateLimitOptions { MaxMessagesPerMinute = 5 }), store);

        // The store reports full on the first read, then the window "rolls" (TTL expiry → 0)
        // before the next poll, so the call waits ~1 step then consumes a fresh slot.
        store.OnGet = key => { store.Counts[key] = 0; };

        var delay = await limiter.DelayForPerMinuteAsync("s1");

        Assert.True(delay >= TimeSpan.FromSeconds(1));
        Assert.Equal(1, store.Counts["botwire:rl:msgmin:s1"]); // admitted after the window freed
    }

    [Fact]
    public async Task TokenBudget_DistributedStore_ExhaustsFromSharedCounter()
    {
        var store = new FakeRateLimitStore();
        var clock = new FakeClock(new DateTimeOffset(2026, 6, 17, 10, 0, 0, TimeSpan.Zero));
        using var limiter = new RateLimiter(
            Options.Create(new RateLimitOptions { DailyTokenBudget = 100 }), clock.Now, store);

        Assert.False(await limiter.IsTokenBudgetExhaustedAsync());
        await limiter.AddTokensAsync(100);
        Assert.True(await limiter.IsTokenBudgetExhaustedAsync());
    }

    private sealed class FakeRateLimitStore : IRateLimitStore
    {
        public readonly Dictionary<string, long> Counts = new();
        public Action<string>? OnGet;

        public Task<long> IncrementAsync(string key, long amount, TimeSpan expiry, CancellationToken ct = default)
        {
            Counts.TryGetValue(key, out var current);
            current += amount;
            Counts[key] = current;
            return Task.FromResult(current);
        }

        public Task<long> GetAsync(string key, CancellationToken ct = default)
        {
            Counts.TryGetValue(key, out var current);
            OnGet?.Invoke(key); // mutate after reading, to simulate a window roll between polls
            return Task.FromResult(current);
        }
    }

    private sealed class FakeClock(DateTimeOffset initial)
    {
        private DateTimeOffset _current = initial;
        public DateTimeOffset Now() => _current;
        public void Advance(TimeSpan delta) => _current += delta;
    }
}
