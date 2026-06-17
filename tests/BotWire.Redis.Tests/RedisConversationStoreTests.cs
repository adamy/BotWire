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
using StackExchange.Redis;

namespace BotWire.Redis.Tests;

public sealed class RedisConversationStoreTests : IDisposable
{
    private readonly IConnectionMultiplexer _mux;
    private readonly RedisConversationStore _store;
    private readonly List<string> _keys = [];

    public RedisConversationStoreTests()
    {
        _mux = ConnectionMultiplexer.Connect(SkipIfNoRedisFact.ConnectionString);
        _store = MakeStore(_mux);
    }

    public void Dispose()
    {
        // Clean up test keys so tests are idempotent.
        if (_keys.Count > 0)
        {
            var db = _mux.GetDatabase();
            db.KeyDelete([.. _keys.Select(k => (RedisKey)$"botwire:session:{k}")]);
        }
        _mux.Dispose();
    }

    private static RedisConversationStore MakeStore(IConnectionMultiplexer mux, TimeSpan? ttl = null, int maxHistory = 50) =>
        new(mux,
            Options.Create(new ConversationStoreOptions { SessionTtl = ttl ?? TimeSpan.FromMinutes(30), MaxHistoryMessages = maxHistory }),
            NullLogger<RedisConversationStore>.Instance);

    private string Token(string suffix)
    {
        var t = $"test:{Guid.NewGuid():N}:{suffix}";
        _keys.Add(t);
        return t;
    }

    private static ConversationSession Session(params ChatMessage[] messages) =>
        new([.. messages], [.. messages], DateTimeOffset.UtcNow);

    private static ChatMessage User(string text) => new(ChatRole.User, text);

    // ── basic CRUD ───────────────────────────────────────────────────────────────

    [SkipIfNoRedisFact]
    public async Task GetAsync_UnknownToken_ReturnsNull()
    {
        var result = await _store.GetAsync(Token("missing"));
        Assert.Null(result);
    }

    [SkipIfNoRedisFact]
    public async Task SaveAndGet_RoundTripsFullSession()
    {
        var token = Token("roundtrip");
        var session = Session(User("hello"));

        await _store.SaveAsync(token, session);
        var loaded = await _store.GetAsync(token);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.SendHistory);
        Assert.Equal("hello", loaded.SendHistory[0].Content);
        Assert.Equal(ChatRole.User, loaded.SendHistory[0].Role);
    }

    [SkipIfNoRedisFact]
    public async Task SaveAsync_UpdatesLastActivity()
    {
        var token = Token("activity");
        var stale = new ConversationSession([User("hi")], [User("hi")], DateTimeOffset.UtcNow.AddDays(-1));
        var before = DateTimeOffset.UtcNow;

        await _store.SaveAsync(token, stale);
        var loaded = await _store.GetAsync(token);

        Assert.NotNull(loaded);
        Assert.True(loaded!.LastActivity >= before);
    }

    [SkipIfNoRedisFact]
    public async Task DeleteAsync_RemovesSession()
    {
        var token = Token("delete");
        await _store.SaveAsync(token, Session(User("hi")));

        await _store.DeleteAsync(token);

        Assert.Null(await _store.GetAsync(token));
    }

    [SkipIfNoRedisFact]
    public async Task DeleteAsync_UnknownToken_DoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(() => _store.DeleteAsync(Token("nope")));
        Assert.Null(ex);
    }

    // ── session flags ────────────────────────────────────────────────────────────

    [SkipIfNoRedisFact]
    public async Task SaveAndGet_PreservesSessionFlags()
    {
        var token = Token("flags");
        var contact = new ContactInfo("user@test.com", null, "Test User");
        var session = new ConversationSession(
            FullHistory:  [User("help")],
            SendHistory:  [User("help")],
            LastActivity: DateTimeOffset.UtcNow,
            EscalationPending:         true,
            EscalationTriggerMessage:  "help",
            KnownUser:                 contact,
            ConsecutiveNoControlWordCount: 2);

        await _store.SaveAsync(token, session);
        var loaded = await _store.GetAsync(token);

        Assert.NotNull(loaded);
        Assert.True(loaded!.EscalationPending);
        Assert.Equal("help", loaded.EscalationTriggerMessage);
        Assert.Equal("user@test.com", loaded.KnownUser?.Email);
        Assert.Equal("Test User", loaded.KnownUser?.Name);
        Assert.Equal(2, loaded.ConsecutiveNoControlWordCount);
    }

    // ── dual-history (Task 22 compression path) ──────────────────────────────────

    [SkipIfNoRedisFact]
    public async Task CompressedSession_DualHistoryPreserved()
    {
        var token = Token("dualhistory");
        var full = new List<ChatMessage>
        {
            User("m1"), User("m2"), User("m3"), User("m4"), User("m5"),
        };
        // SendHistory is compressed: summary + recent turns only.
        var send = new List<ChatMessage>
        {
            new(ChatRole.System, "[summary of m1-m3]"),
            User("m4"),
            User("m5"),
        };
        var session = new ConversationSession(full, send, DateTimeOffset.UtcNow);

        await _store.SaveAsync(token, session);
        var loaded = await _store.GetAsync(token);

        Assert.NotNull(loaded);
        Assert.Equal(5, loaded!.FullHistory.Count);
        Assert.Equal(3, loaded.SendHistory.Count);
        Assert.Equal(ChatRole.System, loaded.SendHistory[0].Role);
        Assert.Equal("[summary of m1-m3]", loaded.SendHistory[0].Content);
        Assert.Equal("m5", loaded.SendHistory[2].Content);
    }

    // ── history cap parity with InMemoryConversationStore ────────────────────────

    [SkipIfNoRedisFact]
    public async Task SaveAsync_TrimsSendHistoryToMaxHistory_KeepingSystemAndFullHistory()
    {
        var token = Token("trim");
        var store = MakeStore(_mux, maxHistory: 3);
        var session = new ConversationSession(
            FullHistory: [User("m1"), User("m2"), User("m3"), User("m4"), User("m5")],
            SendHistory:
            [
                new ChatMessage(ChatRole.System, "rules"),
                User("m1"), User("m2"), User("m3"), User("m4"),
            ],
            LastActivity: DateTimeOffset.UtcNow);

        await store.SaveAsync(token, session);
        var loaded = await store.GetAsync(token);

        Assert.NotNull(loaded);
        // SendHistory capped at 3, system message preserved, oldest user messages dropped.
        Assert.Equal(3, loaded!.SendHistory.Count);
        Assert.Equal(ChatRole.System, loaded.SendHistory[0].Role);
        Assert.Equal("m3", loaded.SendHistory[1].Content);
        Assert.Equal("m4", loaded.SendHistory[2].Content);
        // FullHistory is never trimmed.
        Assert.Equal(5, loaded.FullHistory.Count);
    }

    // ── cross-instance (Task 33 AC 7) ────────────────────────────────────────────

    [SkipIfNoRedisFact]
    public async Task TwoStores_SameRedis_ShareSession()
    {
        var token = Token("crossinstance");
        var storeA = MakeStore(_mux);
        var storeB = MakeStore(_mux);

        await storeA.SaveAsync(token, Session(User("from A")));
        var loaded = await storeB.GetAsync(token);

        Assert.NotNull(loaded);
        Assert.Equal("from A", loaded!.SendHistory[0].Content);
    }

    // ── TTL (Task 33 AC 6) ────────────────────────────────────────────────────────

    [SkipIfNoRedisFact]
    public async Task SaveAsync_RefreshesTtl()
    {
        var token = Token("ttl");
        await _store.SaveAsync(token, Session(User("first")));

        var db = _mux.GetDatabase();
        var ttlAfterFirst = await db.KeyTimeToLiveAsync($"botwire:session:{token}");
        Assert.NotNull(ttlAfterFirst);
        Assert.True(ttlAfterFirst!.Value.TotalSeconds > 0);

        await Task.Delay(100);
        await _store.SaveAsync(token, Session(User("second")));
        var ttlAfterSecond = await db.KeyTimeToLiveAsync($"botwire:session:{token}");

        // TTL was refreshed — should be >= the first TTL (within clock resolution).
        Assert.NotNull(ttlAfterSecond);
        Assert.True(ttlAfterSecond!.Value >= ttlAfterFirst.Value - TimeSpan.FromSeconds(2));
    }
}
