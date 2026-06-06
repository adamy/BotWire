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

namespace BotWire.Core.Ticket;

/// <summary>
/// Thread-safe generator for support ticket identifiers.
/// Format: <c>{prefix}-{yyyyMMdd}-{seq:D4}</c>, e.g. <c>TKT-20260606-0001</c>.
/// The sequence counter resets on process restart (Phase 1 — no persistence needed).
/// </summary>
internal static class TicketIdGenerator
{
    private static long _counter;

    /// <summary>Returns the next unique ticket identifier using the supplied prefix, today's date, and an incrementing sequence.</summary>
    public static string Next(string prefix)
    {
        var seq = Interlocked.Increment(ref _counter);
        return $"{prefix}-{DateTimeOffset.UtcNow:yyyyMMdd}-{seq:D4}";
    }
}
