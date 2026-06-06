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

namespace BotWire.Core.Abstractions;

/// <summary>Inspects user messages for personally identifiable information (PII) and blocks them if found.</summary>
public interface IPiiGuard
{
    /// <summary>True when PII checking is active; false when the guard is a no-op.</summary>
    bool IsEnabled { get; }

    /// <summary>Checks <paramref name="message"/> for PII patterns.</summary>
    /// <param name="message">The raw user message text to inspect.</param>
    /// <returns>A <see cref="PiiCheckResult"/> indicating whether the message was blocked and which pattern matched.</returns>
    PiiCheckResult Check(string message);
}

/// <summary>Result of a <see cref="IPiiGuard.Check"/> call.</summary>
/// <param name="Blocked">True if the message was blocked because a PII pattern matched.</param>
/// <param name="MatchedPattern">The name of the first pattern that matched, or <see langword="null"/> when not blocked.</param>
public sealed record PiiCheckResult(bool Blocked, string? MatchedPattern);
