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
using BotWire.Core.Abstractions;
using BotWire.Core.Enums;
using BotWire.Core.Guard;
using BotWire.Core.Models;
using Microsoft.Extensions.Options;
using NullPromptInjectionGuard = BotWire.Core.Guard.NullPromptInjectionGuard;

namespace BotWire.AspNetCore.Tests;

public class BotWireChatServiceAuditTests
{
    private static (BotWireChatService svc, FakeAuditLogger audit) CreateAudited(
        FakeAnswerProvider? answers = null, int maxRpm = 1000, bool piiBlocks = false)
    {
        var store = new FakeConversationStore();
        answers ??= new FakeAnswerProvider();
        var audit = new FakeAuditLogger();
        var rateLimiter = new IpRateLimiter(
            Options.Create(new RateLimiterOptions { MaxRequestsPerIpPerMinute = maxRpm }));

        // Five-dimension limiter disabled so it never affects audit-trail assertions here.
        var rlOptions = Options.Create(new RateLimitOptions
        {
            MaxConcurrentSessions = 0,
            MaxMessagesPerMinute = 0,
            MaxMessagesPerSession = 0,
            MaxSessionsPerIpPerHour = 0,
            DailyTokenBudget = 0,
        });

        var svc = new BotWireChatService(
            answers,
            store,
            new FakeSessionTokenService(),
            piiBlocks ? FakePiiGuard.Block : FakePiiGuard.Allow,
            NullPromptInjectionGuard.Instance,
            rateLimiter,
            new RateLimiter(rlOptions),
            rlOptions,
            new FakeSummaryCompressor(),
            audit,
            Options.Create(new BotWireOptions()),
            Options.Create(new PiiGuardOptions()));

        return (svc, audit);
    }

    private static ChatRequest ChatReq(string msg = "Hello") => new() { Message = msg };

    private static string? Field(AuditEvent evt, string key) =>
        evt.Data.TryGetValue(key, out var v) ? v?.ToString() : null;

    [Fact]
    public async Task AnswerAsync_Answered_LogsUserThenAssistantMessage()
    {
        var (svc, audit) = CreateAudited();

        await svc.AnswerAsync(ChatReq("How do I reset my password?"), "1.2.3.4");

        var messages = audit.OfType(AuditEventType.Message);
        Assert.Equal(2, messages.Count);
        Assert.Equal("user", Field(messages[0], "role"));
        Assert.Equal("How do I reset my password?", Field(messages[0], "content"));
        Assert.Equal("assistant", Field(messages[1], "role"));
        Assert.Equal("Test answer", Field(messages[1], "content"));
        Assert.Empty(audit.OfType(AuditEventType.Escalated));
    }

    [Fact]
    public async Task AnswerAsync_TicketCreated_LogsEscalatedWithTicketId()
    {
        var answers = new FakeAnswerProvider { Result = new(AnswerStatus.TicketCreated, "TKT-20260611-0001") };
        var (svc, audit) = CreateAudited(answers);

        await svc.AnswerAsync(ChatReq(), "1.2.3.4");

        var escalated = Assert.Single(audit.OfType(AuditEventType.Escalated));
        Assert.Equal("NEED_HUMAN", Field(escalated, "reason"));
        Assert.Equal("TKT-20260611-0001", Field(escalated, "ticketId"));
    }

    [Fact]
    public async Task AnswerAsync_NeedHuman_LogsEscalatedWithoutTicket()
    {
        var answers = new FakeAnswerProvider { Result = new(AnswerStatus.NeedHuman, "Please share your email.") };
        var (svc, audit) = CreateAudited(answers);

        await svc.AnswerAsync(ChatReq(), "1.2.3.4");

        var escalated = Assert.Single(audit.OfType(AuditEventType.Escalated));
        Assert.Equal("NEED_HUMAN", Field(escalated, "reason"));
        Assert.Null(Field(escalated, "ticketId"));
    }

    [Fact]
    public async Task AnswerAsync_PiiBlocked_LogsGuardBlocked_NoMessageEvents()
    {
        var (svc, audit) = CreateAudited(piiBlocks: true);

        await svc.AnswerAsync(ChatReq("my card is 4111 1111 1111 1111"), "1.2.3.4");

        var blocked = Assert.Single(audit.OfType(AuditEventType.GuardBlocked));
        Assert.Equal("pii", Field(blocked, "guard"));
        Assert.Empty(audit.OfType(AuditEventType.Message));
    }

    [Fact]
    public async Task InitSessionAsync_RateLimited_LogsRateLimited()
    {
        var (svc, audit) = CreateAudited(maxRpm: 0);

        await svc.InitSessionAsync(new InitSessionRequest(), "1.2.3.4");

        var limited = Assert.Single(audit.OfType(AuditEventType.RateLimited));
        Assert.Equal("MaxRequestsPerIpPerMinute", Field(limited, "limit"));
    }

    [Fact]
    public async Task AnswerAsync_ProviderThrows_LogsErrorAndRethrows()
    {
        var answers = new FakeAnswerProvider { ThrowError = new InvalidOperationException("provider down") };
        var (svc, audit) = CreateAudited(answers);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AnswerAsync(ChatReq(), "1.2.3.4"));

        var error = Assert.Single(audit.OfType(AuditEventType.Error));
        Assert.Equal("provider down", Field(error, "message"));
    }
}
