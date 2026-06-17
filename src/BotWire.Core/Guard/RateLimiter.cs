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
/// In-memory implementation of the five rate-limiting dimensions in <see cref="RateLimitOptions"/>.
/// Each dimension is independent and disabled when its option is <c>0</c>. Counters are per-process
/// by default; supply an <see cref="IRateLimitStore"/> (e.g. the Redis implementation from
/// <c>BotWire.Redis</c>) to share counters across containers.
/// Thread-safe: per-key windows are guarded by a lock on the window instance; token totals by a
/// dedicated lock.
/// </summary>
public sealed class RateLimiter : IDisposable
{
    private readonly RateLimitOptions _options;
    private readonly Func<DateTimeOffset> _clock;
    private readonly IRateLimitStore? _store;

    private readonly SemaphoreSlim? _concurrency;
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _perMinute = new();
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _perIpHour = new();

    private readonly object _tokenLock = new();
    private long _tokensToday;
    private DateTime _tokenDayUtc;

    /// <summary>Initializes the limiter using the system clock and an optional distributed counter store.</summary>
    public RateLimiter(IOptions<RateLimitOptions> options, IRateLimitStore? store = null)
        : this(options, () => DateTimeOffset.UtcNow, store) { }

    /// <summary>Initializes the limiter with an injectable clock (for testing).</summary>
    internal RateLimiter(IOptions<RateLimitOptions> options, Func<DateTimeOffset> clock, IRateLimitStore? store = null)
    {
        _options = options.Value;
        _clock = clock;
        _store = store;
        _tokenDayUtc = clock().UtcDateTime.Date;
        _concurrency = _options.MaxConcurrentSessions > 0
            ? new SemaphoreSlim(_options.MaxConcurrentSessions, _options.MaxConcurrentSessions)
            : null;
    }

    // ── 1. MaxConcurrentSessions — queue/wait ───────────────────────────────────

    /// <summary>
    /// Acquires a concurrency slot, awaiting one when all are in use. Dispose the returned handle
    /// to release the slot. A no-op handle is returned when the limit is disabled.
    /// </summary>
    public async Task<IDisposable> AcquireConcurrencySlotAsync(CancellationToken ct = default)
    {
        if (_concurrency is null) return NoopReleaser.Instance;
        await _concurrency.WaitAsync(ct).ConfigureAwait(false);
        return new SemaphoreReleaser(_concurrency);
    }

    // ── 2. MaxMessagesPerMinute — delay, no error ───────────────────────────────

    /// <summary>
    /// Throttles a session to <see cref="RateLimitOptions.MaxMessagesPerMinute"/> by delaying the
    /// call when the per-minute quota is spent. Returns the delay that was applied.
    /// When a distributed <see cref="IRateLimitStore"/> is configured, uses a fixed-window Redis
    /// counter instead of the in-process sliding window.
    /// </summary>
    public async Task<TimeSpan> DelayForPerMinuteAsync(string sessionId, CancellationToken ct = default)
    {
        var max = _options.MaxMessagesPerMinute;
        if (_store != null && max > 0)
        {
            // Fixed 60s window. When the window is full, wait for it to roll (the counter
            // resets when its TTL expires) before consuming a slot — this bounds throughput to
            // max per window, mirroring the in-process "delay, never reject" contract rather than
            // admitting every over-quota message after a flat delay. A small cross-instance race
            // (two callers reading max-1 simultaneously) can admit one extra; acceptable for a
            // throttle. The slot is consumed by GetOrAdd-style read-then-increment.
            var key = $"botwire:rl:msgmin:{sessionId}";
            var waited = TimeSpan.Zero;
            var step = TimeSpan.FromSeconds(1);
            while (await _store.GetAsync(key, ct).ConfigureAwait(false) >= max)
            {
                await Task.Delay(step, ct).ConfigureAwait(false);
                waited += step;
            }
            await _store.IncrementAsync(key, 1, TimeSpan.FromSeconds(60), ct).ConfigureAwait(false);
            return waited;
        }

        var delay = NextPerMinuteDelay(sessionId);
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, ct).ConfigureAwait(false);
        return delay;
    }

    /// <summary>
    /// Records a send for <paramref name="sessionId"/> and returns how long the caller should wait
    /// before processing it (zero when within quota). Pure scheduling decision — the actual delay is
    /// applied by <see cref="DelayForPerMinuteAsync"/>; split out so it can be unit-tested without
    /// real time passing.
    /// </summary>
    internal TimeSpan NextPerMinuteDelay(string sessionId)
    {
        var max = _options.MaxMessagesPerMinute;
        if (max <= 0) return TimeSpan.Zero;

        var window = _perMinute.GetOrAdd(sessionId, _ => new Queue<DateTimeOffset>());
        var now = _clock();
        lock (window)
        {
            var cutoff = now.AddMinutes(-1);
            while (window.Count > 0 && window.Peek() <= cutoff)
                window.Dequeue();

            if (window.Count < max)
            {
                window.Enqueue(now);
                return TimeSpan.Zero;
            }

            // Quota spent: reserve the next slot one minute after the oldest send.
            var oldest = window.Dequeue();
            var readyAt = oldest.AddMinutes(1);
            var delay = readyAt - now;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
            window.Enqueue(now + delay);
            return delay;
        }
    }

    // ── 3. MaxMessagesPerSession — prompt new conversation ──────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when a session with <paramref name="sessionMessageCount"/>
    /// user messages has reached the per-session cap.
    /// </summary>
    public bool IsSessionOverMessageCap(int sessionMessageCount)
    {
        var max = _options.MaxMessagesPerSession;
        return max > 0 && sessionMessageCount >= max;
    }

    // ── 4. MaxSessionsPerIpPerHour — reject ─────────────────────────────────────

    /// <summary>
    /// Records a new-session attempt from <paramref name="ip"/> and returns <see langword="false"/>
    /// when the per-IP hourly cap is exceeded (the caller should reject session creation).
    /// </summary>
    public bool TryRegisterIpSession(string ip)
    {
        var max = _options.MaxSessionsPerIpPerHour;
        if (max <= 0) return true;

        var window = _perIpHour.GetOrAdd(ip, _ => new Queue<DateTimeOffset>());
        var now = _clock();
        lock (window)
        {
            var cutoff = now.AddHours(-1);
            while (window.Count > 0 && window.Peek() <= cutoff)
                window.Dequeue();

            if (window.Count >= max) return false;
            window.Enqueue(now);
            return true;
        }
    }

    /// <summary>
    /// Async variant of <see cref="TryRegisterIpSession"/>. When a distributed
    /// <see cref="IRateLimitStore"/> is configured, uses a fixed-window Redis counter.
    /// </summary>
    public async Task<bool> TryRegisterIpSessionAsync(string ip, CancellationToken ct = default)
    {
        var max = _options.MaxSessionsPerIpPerHour;
        if (_store != null && max > 0)
        {
            var count = await _store.IncrementAsync(
                $"botwire:rl:ip:{ip}", 1, TimeSpan.FromHours(1), ct).ConfigureAwait(false);
            return count <= max;
        }
        return TryRegisterIpSession(ip);
    }

    // ── 5. DailyTokenBudget — degraded response ─────────────────────────────────

    /// <summary>Returns <see langword="true"/> when today's token usage has reached the budget.</summary>
    public bool IsTokenBudgetExhausted()
    {
        var budget = _options.DailyTokenBudget;
        if (budget <= 0) return false;
        lock (_tokenLock)
        {
            RollDayIfNeeded();
            return _tokensToday >= budget;
        }
    }

    /// <summary>
    /// Async variant of <see cref="IsTokenBudgetExhausted"/>. When a distributed
    /// <see cref="IRateLimitStore"/> is configured, reads today's total from Redis.
    /// </summary>
    public async Task<bool> IsTokenBudgetExhaustedAsync(CancellationToken ct = default)
    {
        var budget = _options.DailyTokenBudget;
        if (budget <= 0) return false;
        if (_store != null)
        {
            var key = $"botwire:rl:tokens:{_clock().UtcDateTime:yyyyMMdd}";
            var total = await _store.GetAsync(key, ct).ConfigureAwait(false);
            return total >= budget;
        }
        return IsTokenBudgetExhausted();
    }

    /// <summary>Adds <paramref name="tokens"/> to today's running total (resets at midnight UTC).</summary>
    public void AddTokens(int tokens)
    {
        if (_options.DailyTokenBudget <= 0 || tokens <= 0) return;
        lock (_tokenLock)
        {
            RollDayIfNeeded();
            _tokensToday += tokens;
        }
    }

    /// <summary>
    /// Async variant of <see cref="AddTokens"/>. When a distributed <see cref="IRateLimitStore"/>
    /// is configured, atomically increments today's Redis counter with a TTL of seconds until next
    /// UTC midnight.
    /// </summary>
    public async Task AddTokensAsync(int tokens, CancellationToken ct = default)
    {
        if (_options.DailyTokenBudget <= 0 || tokens <= 0) return;
        if (_store != null)
        {
            var now = _clock();
            var midnight = now.UtcDateTime.Date.AddDays(1);
            var ttl = midnight - now.UtcDateTime;
            var key = $"botwire:rl:tokens:{now.UtcDateTime:yyyyMMdd}";
            await _store.IncrementAsync(key, tokens, ttl, ct).ConfigureAwait(false);
            return;
        }
        AddTokens(tokens);
    }

    /// <summary>Today's accumulated token total (for diagnostics/tests).</summary>
    internal long TokensUsedToday
    {
        get { lock (_tokenLock) { RollDayIfNeeded(); return _tokensToday; } }
    }

    private void RollDayIfNeeded()
    {
        var today = _clock().UtcDateTime.Date;
        if (today != _tokenDayUtc)
        {
            _tokenDayUtc = today;
            _tokensToday = 0;
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _concurrency?.Dispose();

    private sealed class SemaphoreReleaser(SemaphoreSlim semaphore) : IDisposable
    {
        private SemaphoreSlim? _semaphore = semaphore;
        public void Dispose()
        {
            // Guard against double-dispose releasing the semaphore twice.
            var s = Interlocked.Exchange(ref _semaphore, null);
            s?.Release();
        }
    }

    private sealed class NoopReleaser : IDisposable
    {
        public static readonly NoopReleaser Instance = new();
        public void Dispose() { }
    }
}
