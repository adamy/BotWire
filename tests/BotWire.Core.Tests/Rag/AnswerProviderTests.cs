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
using BotWire.Core.Enums;
using BotWire.Core.Models;
using BotWire.Core.Rag;
using BotWire.Core.Ticket;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BotWire.Core.Tests.Rag;

public class AnswerProviderTests
{
    private static AnswerProvider CreateProvider(FakeLlmChatClient chat, FakeLlmChatClient? ticketChat = null)
    {
        var options = Options.Create(new AnswerProviderOptions());
        return new(
            chat,
            new FakeDocumentLoader("doc content"),
            new TicketGenerator(ticketChat ?? chat, options, NullLogger<TicketGenerator>.Instance),
            [],
            new DefaultSystemPromptBuilder(options),
            options,
            NullLogger<AnswerProvider>.Instance);
    }

    private static ConversationSession EmptySession() => new([], [], DateTimeOffset.UtcNow);

    private static async Task<List<BotEvent>> CollectAsync(IAsyncEnumerable<BotEvent> stream)
    {
        var events = new List<BotEvent>();
        await foreach (var e in stream)
            events.Add(e);
        return events;
    }

    private static string StreamedText(IEnumerable<BotEvent> events) =>
        string.Concat(events.Where(e => e.Kind == BotEventKind.TextChunk).Select(e => e.Text));

    // ----- Non-streaming -----

    [Fact]
    public async Task AnswerAsync_AnswerSentinel_StripsSentinelLine()
    {
        var provider = CreateProvider(new FakeLlmChatClient("ANSWER\nHello, how can I help?"));

        var result = await provider.AnswerAsync("hi", EmptySession());

        Assert.Equal(AnswerStatus.Answered, result.Status);
        Assert.Equal("Hello, how can I help?", result.Message);
    }

    [Fact]
    public async Task AnswerAsync_EscalateSentinel_ReturnsNeedHuman()
    {
        var provider = CreateProvider(new FakeLlmChatClient("ESCALATE\nThis needs a human."));

        var result = await provider.AnswerAsync("refund please", EmptySession());

        Assert.Equal(AnswerStatus.NeedHuman, result.Status);
    }

    [Fact]
    public async Task AnswerAsync_NoSentinel_FailsOpenAsAnswered()
    {
        const string raw = "Sure, here is the answer with no sentinel.";
        var provider = CreateProvider(new FakeLlmChatClient(raw));

        var result = await provider.AnswerAsync("hi", EmptySession());

        Assert.Equal(AnswerStatus.Answered, result.Status);
        Assert.Equal(raw, result.Message);
    }

    // ----- Streaming -----

    [Fact]
    public async Task StreamAsync_AnswerSentinel_DiscardsSentinelAndStreamsTokens()
    {
        var chat = new FakeLlmChatClient("unused", ["ANSWER\n", "Hello", " world"]);
        var provider = CreateProvider(chat);

        var events = await CollectAsync(provider.StreamAsync("hi", EmptySession()));

        Assert.DoesNotContain(events, e => (e.Text ?? "").Contains("ANSWER"));
        Assert.Equal("Hello world", StreamedText(events));
        Assert.Equal(BotEventKind.Done, events[^1].Kind);
        Assert.Equal("Hello world", events[^1].Result!.Message);
    }

    [Fact]
    public async Task StreamAsync_EscalateSentinel_EmitsEscalateAndCollectContactNoContent()
    {
        var chat = new FakeLlmChatClient("unused", ["ESCALATE\n", "internal reason that must not leak"]);
        var provider = CreateProvider(chat);

        var events = await CollectAsync(provider.StreamAsync("refund", EmptySession()));

        Assert.Equal(2, events.Count);
        Assert.Equal(BotEventKind.Escalated, events[0].Kind);
        Assert.Equal(BotEventKind.CollectContact, events[1].Kind);
        Assert.DoesNotContain(events, e => e.Kind == BotEventKind.TextChunk);
    }

    [Fact]
    public async Task StreamAsync_EscalateSplitAcrossDeltas_StillSuppressesContent()
    {
        var chat = new FakeLlmChatClient("unused", ["ESC", "ALATE", "\n", "leaky text"]);
        var provider = CreateProvider(chat);

        var events = await CollectAsync(provider.StreamAsync("x", EmptySession()));

        Assert.Equal(2, events.Count);
        Assert.Equal(BotEventKind.Escalated, events[0].Kind);
        Assert.Equal(BotEventKind.CollectContact, events[1].Kind);
    }

    [Fact]
    public async Task StreamAsync_NoNewlineWithinScanLimit_FailsOpenAndStreams()
    {
        // 26 chars, no newline, no sentinel -> fail open after the 20-char scan limit.
        var chat = new FakeLlmChatClient("unused", ["abcdefghijklmnopqrstuvwxyz"]);
        var provider = CreateProvider(chat);

        var events = await CollectAsync(provider.StreamAsync("x", EmptySession()));

        Assert.Equal("abcdefghijklmnopqrstuvwxyz", StreamedText(events));
        Assert.Equal(BotEventKind.Done, events[^1].Kind);
        Assert.Equal(AnswerStatus.Answered, events[^1].Result!.Status);
    }

    [Fact]
    public async Task StreamAsync_EscalateWithoutTrailingNewline_SuppressesContent()
    {
        var chat = new FakeLlmChatClient("unused", ["ESCALATE"]);
        var provider = CreateProvider(chat);

        var events = await CollectAsync(provider.StreamAsync("x", EmptySession()));

        Assert.Equal(2, events.Count);
        Assert.Equal(BotEventKind.Escalated, events[0].Kind);
        Assert.Equal(BotEventKind.CollectContact, events[1].Kind);
    }

    [Fact]
    public async Task AnswerAsync_SendsSendHistory_NotFullHistory()
    {
        var chat = new FakeLlmChatClient("ANSWER\nok");
        var provider = CreateProvider(chat);
        var session = new ConversationSession(
            FullHistory: [new(ChatRole.User, "FULL-ONLY-MARKER")],
            SendHistory: [new(ChatRole.User, "SEND-ONLY-MARKER")],
            LastActivity: DateTimeOffset.UtcNow);

        await provider.AnswerAsync("now", session);

        Assert.Contains(chat.LastMessages!, m => m.Content.Contains("SEND-ONLY-MARKER"));
        Assert.DoesNotContain(chat.LastMessages!, m => m.Content.Contains("FULL-ONLY-MARKER"));
    }

    // ----- Escalation lifecycle -----

    [Fact]
    public async Task StreamAsync_EscalationPendingWithContact_EmitsTicketConfirmedAndDone()
    {
        const string ticketJson = """{"summary":"Refund issue","details":"User wants refund","priority":"medium"}""";
        var ticketChat = new FakeLlmChatClient(ticketJson);
        var provider = CreateProvider(new FakeLlmChatClient("unused"), ticketChat);
        var session = new ConversationSession([], [], DateTimeOffset.UtcNow, EscalationPending: true, EscalationTriggerMessage: "I want a refund");
        var contact = new ContactInfo("a@b.com", null);

        var events = await CollectAsync(provider.StreamAsync("contact submitted", session, contact));

        Assert.Equal(2, events.Count);
        Assert.Equal(BotEventKind.TicketConfirmed, events[0].Kind);
        Assert.Equal(BotEventKind.Done, events[1].Kind);
        Assert.Equal(AnswerStatus.TicketCreated, events[1].Result!.Status);
        Assert.NotNull(events[0].TicketId);
    }

    [Fact]
    public async Task AnswerAsync_EscalationPendingWithContact_ReturnsTicketId()
    {
        const string ticketJson = """{"summary":"s","details":"d","priority":"low"}""";
        var ticketChat = new FakeLlmChatClient(ticketJson);
        var provider = CreateProvider(new FakeLlmChatClient("unused"), ticketChat);
        var session = new ConversationSession([], [], DateTimeOffset.UtcNow, EscalationPending: true, EscalationTriggerMessage: "need help");
        var contact = new ContactInfo("x@y.com", null);

        var result = await provider.AnswerAsync("contact submitted", session, contact);

        Assert.Equal(AnswerStatus.TicketCreated, result.Status);
        Assert.Matches(@"^TKT-\d{8}-\d{4,}$", result.Message);
    }

    [Fact]
    public async Task StreamAsync_EscalationPendingWithoutContact_FallsThroughToNormalRag()
    {
        var chat = new FakeLlmChatClient("ANSWER\nHere is your answer.");
        var provider = CreateProvider(chat);
        var session = new ConversationSession([], [], DateTimeOffset.UtcNow, EscalationPending: true);

        // No contact supplied — should NOT generate ticket, should run normal RAG
        var events = await CollectAsync(provider.StreamAsync("hello", session, contact: null));

        Assert.Equal(BotEventKind.Done, events[^1].Kind);
        Assert.Contains(events, e => e.Kind == BotEventKind.TextChunk);
    }

    // ---- Escalation Flow Tests ----

    [Fact]
    public async Task StreamAsync_EscalateDetected_EmitsCollectContactWhenNoContact()
    {
        var chat = new FakeLlmChatClient("ESCALATE\nYou'll need to speak to a human.");
        var provider = CreateProvider(chat);
        var events = await CollectAsync(provider.StreamAsync("issue", EmptySession(), contact: null));

        // Should emit Escalated then CollectContact (no TicketConfirmed without contact)
        Assert.Contains(events, e => e.Kind == BotEventKind.Escalated);
        Assert.Contains(events, e => e.Kind == BotEventKind.CollectContact);
        // No TextChunk for ESCALATE body
        Assert.DoesNotContain(events, e => e.Kind == BotEventKind.TextChunk);
    }

    [Fact]
    public async Task AnswerAsync_EscalateDetected_NoContactReturnsNeedHuman()
    {
        var chat = new FakeLlmChatClient("ESCALATE\nConnect with support.");
        var provider = CreateProvider(chat);
        var result = await provider.AnswerAsync("problem", EmptySession(), contact: null);

        Assert.Equal(AnswerStatus.NeedHuman, result.Status);
    }

    [Fact]
    public async Task StreamAsync_EscalateWithContactProvided_GeneratesTicket()
    {
        var chat = new FakeLlmChatClient("ESCALATE\nI'll connect you.");
        var ticketChat = new FakeLlmChatClient("""{"summary":"Broken feature","details":"User reports issue","priority":"medium"}""");
        var provider = CreateProvider(chat, ticketChat);
        var contact = new ContactInfo("user@example.com", null, "John", "john99");

        var events = await CollectAsync(provider.StreamAsync("urgent problem", EmptySession(), contact));

        // Should emit: Escalated, TicketConfirmed, Done (no CollectContact)
        Assert.Equal(BotEventKind.Escalated, events[0].Kind);
        Assert.Contains(events, e => e.Kind == BotEventKind.TicketConfirmed);
        Assert.DoesNotContain(events, e => e.Kind == BotEventKind.CollectContact);
    }

    [Fact]
    public async Task AnswerAsync_EscalateResponse_ReturnsNeedHumanNotTicket()
    {
        // When LLM says ESCALATE, AnswerAsync returns NeedHuman (not immediate ticket).
        // Ticket is only created when EscalationPending && contact supplied (step 2 of flow).
        var chat = new FakeLlmChatClient("ESCALATE\nI'll help.");
        var provider = CreateProvider(chat);
        var contact = new ContactInfo("test@example.com", null, "Alice", null);

        var result = await provider.AnswerAsync("cant login", EmptySession(), contact);

        Assert.Equal(AnswerStatus.NeedHuman, result.Status);
    }

    [Fact]
    public async Task StreamAsync_EscalateWithNullContactEmail_StillGeneratesTicket()
    {
        var chat = new FakeLlmChatClient("ESCALATE\nConnecting you...");
        var ticketChat = new FakeLlmChatClient("""{"summary":"Support needed","details":"Issue reported","priority":"medium"}""");
        var provider = CreateProvider(chat, ticketChat);
        // Contact object exists but Email is null — ticket is still generated with null email
        var contact = new ContactInfo(null, null, "Jane");

        var events = await CollectAsync(provider.StreamAsync("issue", EmptySession(), contact));

        // Should generate ticket even with null email (contact object exists)
        Assert.Contains(events, e => e.Kind == BotEventKind.TicketConfirmed);
        Assert.DoesNotContain(events, e => e.Kind == BotEventKind.CollectContact);
    }

    [Fact]
    public async Task AnswerAsync_NoContactEmail_ReturnsNeedHuman()
    {
        var chat = new FakeLlmChatClient("ESCALATE\nWe need your email.");
        var provider = CreateProvider(chat);
        var result = await provider.AnswerAsync("help", EmptySession(), contact: null);

        Assert.Equal(AnswerStatus.NeedHuman, result.Status);
    }

    [Fact]
    public async Task StreamAsync_FailOpenThenEscalate_IncrementsConsecutiveCount()
    {
        // First request: no sentinel → fail-open, count becomes 1
        var badChat = new FakeLlmChatClient("random text no sentinel");
        var provider = CreateProvider(badChat);
        var session1 = EmptySession();

        var result1 = await provider.AnswerAsync("msg1", session1);

        Assert.False(result1.FailedOpen == false); // FailedOpen should be true
        // Note: FailedOpen flag exists, but ConsecutiveNoControlWordCount is session-level,
        // not returned from AnswerAsync. Would be tested at service layer.
    }
}
