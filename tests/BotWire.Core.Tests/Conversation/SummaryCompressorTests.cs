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
using BotWire.Core.Conversation;
using BotWire.Core.Enums;
using BotWire.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace BotWire.Core.Tests.Conversation;

public class SummaryCompressorTests
{
    private const string SummaryPrefix = "Previous conversation summary: ";

    private static ChatMessage User(string t) => new(ChatRole.User, t);
    private static ChatMessage Bot(string t) => new(ChatRole.Assistant, t);

    private static SummaryCompressor Create(CapturingChat chat) =>
        new(chat, NullLogger<SummaryCompressor>.Instance);

    [Fact]
    public async Task BelowTwiceInterval_ReturnsUnchanged_NoLlmCall()
    {
        var chat = new CapturingChat("SUMMARY");
        var compressor = Create(chat);
        var history = new List<ChatMessage> { User("u1"), Bot("a1"), User("u2") }; // 3 < 2×2

        var result = await compressor.CompressAsync(history, interval: 2);

        Assert.Equal(3, result.Count);
        Assert.Equal(0, chat.Calls);
        Assert.DoesNotContain(result, m => m.Content.StartsWith(SummaryPrefix, StringComparison.Ordinal));
    }

    [Fact]
    public async Task IntervalZero_Disabled_ReturnsUnchanged_NoLlmCall()
    {
        var chat = new CapturingChat("SUMMARY");
        var compressor = Create(chat);
        var history = new List<ChatMessage> { User("u1"), Bot("a1"), User("u2"), Bot("a2") };

        var result = await compressor.CompressAsync(history, interval: 0);

        Assert.Equal(4, result.Count);
        Assert.Equal(0, chat.Calls);
    }

    [Fact]
    public async Task AtTwiceInterval_FoldsOldest_KeepsLatestIntervalBehindOneSummary()
    {
        var chat = new CapturingChat("SUMMARY");
        var compressor = Create(chat);
        // interval=2, 4 messages → fold oldest 2 (u1,a1), keep latest 2 (u2,a2)
        var history = new List<ChatMessage> { User("u1"), Bot("a1"), User("u2"), Bot("a2") };

        var result = await compressor.CompressAsync(history, interval: 2);

        Assert.Equal(1, chat.Calls);
        Assert.Equal(3, result.Count);
        Assert.Equal(ChatRole.System, result[0].Role);
        Assert.Equal(SummaryPrefix + "SUMMARY", result[0].Content);
        Assert.Equal("u2", result[1].Content);
        Assert.Equal("a2", result[2].Content);
        // The folded (oldest) messages were the ones sent to the summariser.
        Assert.Contains(chat.LastMessages!, m => m.Content == "u1");
        Assert.Contains(chat.LastMessages!, m => m.Content == "a1");
        Assert.DoesNotContain(chat.LastMessages!, m => m.Content == "a2");
    }

    [Fact]
    public async Task PriorSummary_IsFoldedIntoNewSummary_OnlyOneSummaryRemains()
    {
        var chat = new CapturingChat("NEWSUM");
        var compressor = Create(chat);
        // Leading summary + 4 turns (interval=2 → 2×2 threshold on the 4 non-summary messages).
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, SummaryPrefix + "old summary"),
            User("u1"), Bot("a1"), User("u2"), Bot("a2"),
        };

        var result = await compressor.CompressAsync(history, interval: 2);

        var summaries = result.Where(m => m.Content.StartsWith(SummaryPrefix, StringComparison.Ordinal)).ToList();
        Assert.Single(summaries);
        Assert.Equal(SummaryPrefix + "NEWSUM", summaries[0].Content);
        Assert.Equal(3, result.Count); // [summary] + latest 2
        Assert.Equal("u2", result[1].Content);
        Assert.Equal("a2", result[2].Content);
        // Old summary content was handed to the summariser so context is not lost.
        Assert.Contains(chat.LastMessages!, m => m.Content.Contains("old summary"));
    }

    private sealed class CapturingChat(string response) : ILlmChatClient
    {
        public int Calls { get; private set; }
        public IReadOnlyList<ChatMessage>? LastMessages { get; private set; }

        public string Name => "capturing";

        public Task<string> ChatAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastMessages = messages;
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<string> ChatStreamingAsync(
            IReadOnlyList<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return response;
        }
    }
}
