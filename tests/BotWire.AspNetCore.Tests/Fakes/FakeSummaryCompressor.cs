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

using BotWire.Core.Conversation;
using BotWire.Core.Models;

namespace BotWire.AspNetCore.Tests.Fakes;

/// <summary>Pass-through <see cref="ISummaryCompressor"/>: returns the send-history unchanged.</summary>
internal sealed class FakeSummaryCompressor : ISummaryCompressor
{
    public Task<List<ChatMessage>> CompressAsync(
        IReadOnlyList<ChatMessage> sendHistory, int interval, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<ChatMessage>(sendHistory));
}
