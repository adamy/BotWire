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
/// Each dimension is independent and disabled when its option is <c>0</c>. Counters are per-process;
/// a shared store (Redis) for multi-instance deployments is planned for Phase 3.
/// Thread-safe: per-key windows are guarded by a lock on the window instance; token totals by a
/// dedicated lock.
/// </summary>
public sealed class RateLimiter : IDisposable
{
    private readonly RateLimitOptions _options;
    private readonly Func<DateTimeOffset> _clock;

    private readonly SemaphoreSlim? _concurrency;
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _perMinute = new();
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _perIpHour = new();

    private readonly object _tokenLock = new();
    private long _tokensToday;
    private DateTime _tokenDayUtc;

    /// <summary>Initializes the limiter using the system clock.</summary>
    public RateLimiter(IOptions<RateLimitOptions> options)
        : this(options, () => DateTimeOffset.UtcNow) { }

    /// <summary>Initializes the limiter with an injectable clock (for testing).</summary>
    internal RateLimiter(IOptions<RateLimitOptions> options, Func<DateTimeOffset> clock)
    {
        _options = options.Value;
        _clock = clock;
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
    /// </summary>
    public async Task<TimeSpan> DelayForPerMinuteAsync(string sessionId, CancellationToken ct = default)
    {
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

    // ── 5. DailyTokenBudget — degraded response ─────────────────────────────────
    //
    // The running total is per-process and in-memory, so it resets to zero on restart — a
    // process bounce effectively grants a fresh budget. This is acceptable for the single-instance
    // cost guard Phase 1.5 targets; durable, cross-instance budgeting (Redis) lands in Phase 3.

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
