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

public class IpRateLimiterTests
{
    private static IpRateLimiter Create(int max, Func<DateTimeOffset>? clock = null)
    {
        var opts = Options.Create(new RateLimiterOptions { MaxRequestsPerIpPerMinute = max });
        return clock is null ? new IpRateLimiter(opts) : new IpRateLimiter(opts, clock);
    }

    [Fact]
    public void IsAllowed_AllowsUpToMax()
    {
        var limiter = Create(3);
        Assert.True(limiter.IsAllowed("1.2.3.4"));
        Assert.True(limiter.IsAllowed("1.2.3.4"));
        Assert.True(limiter.IsAllowed("1.2.3.4"));
    }

    [Fact]
    public void IsAllowed_BlocksOnMaxPlusOne()
    {
        var limiter = Create(3);
        limiter.IsAllowed("1.2.3.4");
        limiter.IsAllowed("1.2.3.4");
        limiter.IsAllowed("1.2.3.4");
        Assert.False(limiter.IsAllowed("1.2.3.4"));
    }

    [Fact]
    public void IsAllowed_DifferentIps_IndependentWindows()
    {
        var limiter = Create(2);
        limiter.IsAllowed("10.0.0.1");
        limiter.IsAllowed("10.0.0.1");
        // 10.0.0.1 is now exhausted — 10.0.0.2 should still be allowed
        Assert.True(limiter.IsAllowed("10.0.0.2"));
    }

    [Fact]
    public void IsAllowed_AllowsAgainAfterWindowExpires()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new FakeClock(now);
        var limiter = Create(2, clock.Now);

        limiter.IsAllowed("5.5.5.5");
        limiter.IsAllowed("5.5.5.5");
        Assert.False(limiter.IsAllowed("5.5.5.5")); // blocked

        // Advance past the 1-minute window
        clock.Advance(TimeSpan.FromSeconds(61));

        Assert.True(limiter.IsAllowed("5.5.5.5")); // allowed again
    }

    [Fact]
    public void IsAllowed_SlidingWindow_OnlyOldEntriesExpire()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new FakeClock(now);
        var limiter = Create(3, clock.Now);

        limiter.IsAllowed("9.9.9.9"); // t=0
        limiter.IsAllowed("9.9.9.9"); // t=0

        clock.Advance(TimeSpan.FromSeconds(61)); // first two entries now expired
        limiter.IsAllowed("9.9.9.9"); // t=61
        limiter.IsAllowed("9.9.9.9"); // t=61
        Assert.True(limiter.IsAllowed("9.9.9.9")); // t=61 — 3rd in new window, should pass
    }

    private sealed class FakeClock(DateTimeOffset initial)
    {
        private DateTimeOffset _current = initial;
        public DateTimeOffset Now() => _current;
        public void Advance(TimeSpan delta) => _current += delta;
    }
}
