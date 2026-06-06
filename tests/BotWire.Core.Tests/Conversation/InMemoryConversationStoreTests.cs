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
using BotWire.Core.Enums;
using BotWire.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BotWire.Core.Tests.Conversation;

public class InMemoryConversationStoreTests
{
    private static InMemoryConversationStore CreateStore(
        TimeSpan? ttl = null,
        int maxHistory = 50)
    {
        var options = Options.Create(new ConversationStoreOptions
        {
            // Default to a TTL long enough that the background timer never fires during a test;
            // expiry is exercised directly through RemoveExpired.
            SessionTtl = ttl ?? TimeSpan.FromHours(1),
            MaxHistoryMessages = maxHistory,
        });

        return new InMemoryConversationStore(options, NullLogger<InMemoryConversationStore>.Instance);
    }

    private static ConversationSession Session(params ChatMessage[] messages) =>
        new([.. messages], DateTimeOffset.UtcNow);

    private static ChatMessage User(string text) => new(ChatRole.User, text);

    [Fact]
    public async Task GetAsync_UnknownToken_ReturnsNull()
    {
        using var store = CreateStore();

        var result = await store.GetAsync("missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveThenGet_RoundTripsSession()
    {
        using var store = CreateStore();
        var session = Session(User("hello"));

        await store.SaveAsync("t1", session);
        var loaded = await store.GetAsync("t1");

        Assert.NotNull(loaded);
        Assert.Single(loaded!.History);
        Assert.Equal("hello", loaded.History[0].Content);
    }

    [Fact]
    public async Task SaveAsync_UpdatesLastActivity()
    {
        using var store = CreateStore();
        var stale = new ConversationSession([User("hi")], DateTimeOffset.UtcNow.AddDays(-1));
        var before = DateTimeOffset.UtcNow;

        await store.SaveAsync("t1", stale);
        var loaded = await store.GetAsync("t1");

        Assert.NotNull(loaded);
        Assert.True(loaded!.LastActivity >= before);
    }

    [Fact]
    public async Task DeleteAsync_RemovesSession()
    {
        using var store = CreateStore();
        await store.SaveAsync("t1", Session(User("hi")));

        await store.DeleteAsync("t1");

        Assert.Null(await store.GetAsync("t1"));
    }

    [Fact]
    public async Task DeleteAsync_UnknownToken_DoesNotThrow()
    {
        using var store = CreateStore();

        var ex = await Record.ExceptionAsync(() => store.DeleteAsync("nope"));

        Assert.Null(ex);
    }

    [Fact]
    public async Task SaveAsync_TrimsOldestNonSystemMessages_KeepingSystem()
    {
        using var store = CreateStore(maxHistory: 3);
        var session = Session(
            new ChatMessage(ChatRole.System, "rules"),
            User("m1"),
            User("m2"),
            User("m3"),
            User("m4"));

        await store.SaveAsync("t1", session);
        var loaded = await store.GetAsync("t1");

        Assert.NotNull(loaded);
        Assert.Equal(3, loaded!.History.Count);
        // System message preserved at the front, oldest user messages (m1, m2) dropped.
        Assert.Equal(ChatRole.System, loaded.History[0].Role);
        Assert.Equal("rules", loaded.History[0].Content);
        Assert.Equal("m3", loaded.History[1].Content);
        Assert.Equal("m4", loaded.History[2].Content);
    }

    [Fact]
    public void TrimHistory_AllSystemOverCap_ReturnsUnchanged()
    {
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, "a"),
            new(ChatRole.System, "b"),
            new(ChatRole.System, "c"),
        };

        var result = InMemoryConversationStore.TrimHistory(history, max: 2);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void TrimHistory_SystemExceedsCapWithNonSystem_DropsAllNonSystemKeepsSystem()
    {
        // 3 system + 2 user, cap = 2: system messages are never dropped, so all 3 system
        // messages are kept and both user messages are dropped. Result exceeds the cap.
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, "sys1"),
            new(ChatRole.System, "sys2"),
            new(ChatRole.System, "sys3"),
            User("u1"),
            User("u2"),
        };

        var result = InMemoryConversationStore.TrimHistory(history, max: 2);

        Assert.Equal(3, result.Count);
        Assert.All(result, m => Assert.Equal(ChatRole.System, m.Role));
    }

    [Fact]
    public async Task RemoveExpired_RemovesOnlySessionsPastTtl()
    {
        using var store = CreateStore(ttl: TimeSpan.FromMinutes(30));
        var now = DateTimeOffset.UtcNow;

        // SaveAsync stamps LastActivity ≈ now; evaluate the sweep relative to a fixed clock.
        await store.SaveAsync("recent", Session(User("hi")));

        // A sweep 10 minutes later keeps it (within the 30-minute TTL).
        var removedSoon = store.RemoveExpired(now.AddMinutes(10));
        Assert.Equal(0, removedSoon);

        // A sweep 31 minutes later evicts it.
        var removedLate = store.RemoveExpired(now.AddMinutes(31));
        Assert.Equal(1, removedLate);
        Assert.Null(await store.GetAsync("recent"));
    }
}
