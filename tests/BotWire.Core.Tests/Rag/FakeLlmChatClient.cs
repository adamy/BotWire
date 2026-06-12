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

using System.Runtime.CompilerServices;
using BotWire.Core.Abstractions;
using BotWire.Core.Models;

namespace BotWire.Core.Tests.Rag;

/// <summary>Deterministic <see cref="ILlmChatClient"/> for sentinel tests.</summary>
internal sealed class FakeLlmChatClient : ILlmChatClient
{
    private readonly string _full;
    private readonly IReadOnlyList<string> _deltas;

    public FakeLlmChatClient(string full, IReadOnlyList<string>? deltas = null)
    {
        _full = full;
        _deltas = deltas ?? [full];
    }

    public string Name => "fake";

    /// <summary>Messages from the most recent <see cref="ChatAsync"/> call, for assertions.</summary>
    public IReadOnlyList<ChatMessage>? LastMessages { get; private set; }

    public Task<LlmChatResult> ChatAsync(
        IReadOnlyList<ChatMessage> messages, bool jsonObject = false, CancellationToken cancellationToken = default)
    {
        LastMessages = messages;
        return Task.FromResult(new LlmChatResult(_full));
    }

    public async IAsyncEnumerable<string> ChatStreamingAsync(
        IReadOnlyList<ChatMessage> messages,
        bool jsonObject = false,
        Action<int>? onUsage = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var delta in _deltas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return delta;
            await Task.Yield();
        }

        onUsage?.Invoke(0);
    }
}

/// <summary><see cref="IDocumentLoader"/> stub returning a fixed document set without touching disk.</summary>
internal sealed class FakeDocumentLoader : IDocumentLoader
{
    private readonly IReadOnlyList<string> _documents;

    public FakeDocumentLoader(params string[] documents) => _documents = documents;

    public Task<IReadOnlyList<string>> LoadAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default) =>
        Task.FromResult(_documents);
}
