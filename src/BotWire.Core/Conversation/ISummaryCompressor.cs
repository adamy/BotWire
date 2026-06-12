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

using BotWire.Core.Models;

namespace BotWire.Core.Conversation;

/// <summary>
/// Compresses a session's send-history so the token cost of long conversations stays bounded.
/// Folds the oldest turns (and any prior summary) into a single summary system message while
/// preserving the most recent turns verbatim.
/// </summary>
public interface ISummaryCompressor
{
    /// <summary>
    /// Returns a possibly-compressed copy of <paramref name="sendHistory"/>.
    /// Compression fires only when the number of non-summary messages reaches twice
    /// <paramref name="interval"/>; otherwise the input is returned unchanged (copied).
    /// When it fires, the oldest messages are summarised via the LLM into one leading
    /// system message and the most recent <paramref name="interval"/> messages are kept.
    /// An <paramref name="interval"/> of 0 (or less) disables compression entirely.
    /// </summary>
    Task<List<ChatMessage>> CompressAsync(
        IReadOnlyList<ChatMessage> sendHistory,
        int interval,
        CancellationToken cancellationToken = default);
}
