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

using BotWire.Core.Abstractions;
using BotWire.Core.Models;

namespace BotWire.AspNetCore.Tests.Fakes;

internal sealed class FakeConversationStore : IConversationStore
{
    private readonly Dictionary<string, ConversationSession> _data = new();

    public Task<ConversationSession?> GetAsync(string token, CancellationToken cancellationToken = default)
        => Task.FromResult(_data.TryGetValue(token, out var s) ? s : (ConversationSession?)null);

    public Task SaveAsync(string token, ConversationSession session, CancellationToken cancellationToken = default)
    {
        _data[token] = session;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string token, CancellationToken cancellationToken = default)
    {
        _data.Remove(token);
        return Task.CompletedTask;
    }

    public bool Contains(string token) => _data.ContainsKey(token);
    public ConversationSession? Get(string token) => _data.TryGetValue(token, out var s) ? s : null;
}
