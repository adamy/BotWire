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

using BotWire.AspNetCore.Tests.Fakes;
using BotWire.Core.Enums;
using BotWire.Core.Guard;
using BotWire.Core.Models;
using Microsoft.Extensions.Options;

namespace BotWire.AspNetCore.Tests;

public class BotWireChatServiceTests
{
    // ── Factory ──────────────────────────────────────────────────────────────────

    private static (BotWireChatService svc, FakeConversationStore store) Create(
        FakeAnswerProvider? answers = null,
        int maxMsg  = 2000,
        int maxRpm  = 1000,
        bool piiBlocks = false)
    {
        var store   = new FakeConversationStore();
        answers   ??= new FakeAnswerProvider();

        var rateLimiter = new IpRateLimiter(
            Options.Create(new RateLimiterOptions { MaxRequestsPerIpPerMinute = maxRpm }));

        var svc = new BotWireChatService(
            answers,
            store,
            new FakeSessionTokenService(),
            piiBlocks ? FakePiiGuard.Block : FakePiiGuard.Allow,
            rateLimiter,
            Options.Create(new BotWireOptions { MaxMessageLength = maxMsg }),
            Options.Create(new PiiGuardOptions()));

        return (svc, store);
    }

    private static InitSessionRequest SessionReq(
        string? name = null, string? username = null, string? email = null)
        => new() { Name = name, Username = username, Email = email };

    private static ChatRequest ChatReq(
        string msg = "Hello", string? token = null, string? email = null)
        => new() { Message = msg, SessionToken = token, ContactEmail = email };

    // ── InitSessionAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task InitSessionAsync_RateLimited_ReturnsError429()
    {
        var (svc, _) = Create(maxRpm: 0);
        var (error, token, _) = await svc.InitSessionAsync(SessionReq(), "1.2.3.4");
        Assert.NotNull(error);
        Assert.Equal(429, error.HttpStatusCode);
        Assert.Equal("Blocked", error.Status);
        Assert.Equal("", token);
    }

    [Fact]
    public async Task InitSessionAsync_Anonymous_NeedsNameTrue_SessionSaved()
    {
        var (svc, store) = Create();
        var (error, token, needsName) = await svc.InitSessionAsync(SessionReq(), "1.2.3.4");
        Assert.Null(error);
        Assert.False(string.IsNullOrEmpty(token));
        Assert.True(needsName);
        Assert.True(store.Contains(token));
    }

    [Fact]
    public async Task InitSessionAsync_WithName_NeedsNameFalse()
    {
        var (svc, _) = Create();
        var (_, _, needsName) = await svc.InitSessionAsync(SessionReq(name: "Jane"), "1.2.3.4");
        Assert.False(needsName);
    }

    [Fact]
    public async Task InitSessionAsync_WhitespaceOnlyName_TreatedAsNull_NeedsNameTrue()
    {
        var (svc, _) = Create();
        var (_, _, needsName) = await svc.InitSessionAsync(SessionReq(name: "   "), "1.2.3.4");
        Assert.True(needsName);
    }

    [Fact]
    public async Task InitSessionAsync_KnownUser_StoredInSession()
    {
        var (svc, store) = Create();
        var (_, token, _) = await svc.InitSessionAsync(
            SessionReq(name: "Jane", username: "jane99", email: "jane@example.com"), "1.2.3.4");

        var session = store.Get(token);
        Assert.NotNull(session?.KnownUser);
        Assert.Equal("Jane",             session.KnownUser.Name);
        Assert.Equal("jane99",           session.KnownUser.Username);
        Assert.Equal("jane@example.com", session.KnownUser.Email);
    }

    [Fact]
    public async Task InitSessionAsync_WhitespaceEmail_StoredAsNull()
    {
        var (svc, store) = Create();
        var (_, token, _) = await svc.InitSessionAsync(
            SessionReq(username: "u1", email: "  "), "1.2.3.4");

        var session = store.Get(token);
        Assert.Null(session?.KnownUser?.Email);
        Assert.Equal("u1", session?.KnownUser?.Username);
    }

    // ── AnswerAsync ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnswerAsync_MessageTooLong_Returns400()
    {
        var (svc, _) = Create(maxMsg: 10);
        var result = await svc.AnswerAsync(ChatReq("This message is too long"), "1.2.3.4");
        Assert.Equal(400, result.HttpStatusCode);
        Assert.Equal("Blocked", result.Status);
    }

    [Fact]
    public async Task AnswerAsync_RateLimited_Returns429()
    {
        var (svc, _) = Create(maxRpm: 0);
        var result = await svc.AnswerAsync(ChatReq(), "1.2.3.4");
        Assert.Equal(429, result.HttpStatusCode);
        Assert.Equal("Blocked", result.Status);
    }

    [Fact]
    public async Task AnswerAsync_PiiBlocked_Returns400()
    {
        var (svc, _) = Create(piiBlocks: true);
        var result = await svc.AnswerAsync(ChatReq(), "1.2.3.4");
        Assert.Equal(400, result.HttpStatusCode);
        Assert.Equal("Blocked", result.Status);
    }

    [Fact]
    public async Task AnswerAsync_InvalidToken_Returns400()
    {
        var (svc, _) = Create();
        var result = await svc.AnswerAsync(ChatReq(token: "no-such-token"), "1.2.3.4");
        Assert.Equal(400, result.HttpStatusCode);
        Assert.Equal("Blocked", result.Status);
    }

    [Fact]
    public async Task AnswerAsync_Answered_ReturnsAnswerWithToken()
    {
        var answers = new FakeAnswerProvider
        {
            Result = new AnswerResult(AnswerStatus.Answered, "Here is your answer."),
        };
        var (svc, store) = Create(answers);
        var result = await svc.AnswerAsync(ChatReq(), "1.2.3.4");
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Equal("Answered", result.Status);
        Assert.Equal("Here is your answer.", result.Message);
        Assert.False(string.IsNullOrEmpty(result.SessionToken));
        Assert.True(store.Contains(result.SessionToken));
    }

    [Fact]
    public async Task AnswerAsync_NeedHuman_ReturnsNeedHuman()
    {
        var answers = new FakeAnswerProvider
        {
            Result = new AnswerResult(AnswerStatus.NeedHuman, "Please hold."),
        };
        var (svc, _) = Create(answers);
        var result = await svc.AnswerAsync(ChatReq(), "1.2.3.4");
        Assert.Equal("NeedHuman", result.Status);
        Assert.Equal("Please hold.", result.Message);
    }

    // ── PrepareStreamAsync — verifies Fix #1 (new session saved before streaming) ─

    [Fact]
    public async Task PrepareStreamAsync_NewSession_SavedToStoreBeforeStreaming()
    {
        var (svc, store) = Create();
        var prep = await svc.PrepareStreamAsync(ChatReq(token: null), "1.2.3.4");
        Assert.Null(prep.Error);
        Assert.True(store.Contains(prep.Token!));
    }

    // ── CommitStreamAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CommitStreamAsync_Normal_AppendsUserAndBotMessages()
    {
        var (svc, store) = Create();
        var prep = await svc.PrepareStreamAsync(ChatReq("Hi"), "1.2.3.4");
        await svc.CommitStreamAsync(prep, accumulatedText: "Hello!", escalationStarted: false, confirmedTicketId: null);

        var session = store.Get(prep.Token!);
        Assert.Equal(2, session!.History.Count);
        Assert.Equal("Hi",     session.History[0].Content);
        Assert.Equal("Hello!", session.History[1].Content);
        Assert.False(session.EscalationPending);
    }

    [Fact]
    public async Task CommitStreamAsync_EscalationStarted_SetsPending()
    {
        var (svc, store) = Create();
        var prep = await svc.PrepareStreamAsync(ChatReq("I need help"), "1.2.3.4");
        await svc.CommitStreamAsync(prep, accumulatedText: "", escalationStarted: true, confirmedTicketId: null);

        var session = store.Get(prep.Token!);
        Assert.True(session!.EscalationPending);
        Assert.Equal("I need help", session.EscalationTriggerMessage);
    }

    [Fact]
    public async Task CommitStreamAsync_TicketConfirmed_ClearsPendingAndTrigger()
    {
        var (svc, store) = Create();
        var prep = await svc.PrepareStreamAsync(ChatReq("I need help"), "1.2.3.4");
        // Simulate prior escalation on the session
        await svc.CommitStreamAsync(prep, "", escalationStarted: true, confirmedTicketId: null);

        var prep2 = await svc.PrepareStreamAsync(
            ChatReq("my email is x@example.com", token: prep.Token), "1.2.3.4");
        await svc.CommitStreamAsync(prep2, "", escalationStarted: false, confirmedTicketId: "TKT-001");

        var session = store.Get(prep.Token!);
        Assert.False(session!.EscalationPending);
        Assert.Null(session.EscalationTriggerMessage);
    }
}
