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
    private static AnswerProvider CreateProvider(
        FakeLlmChatClient chat, FakeLlmChatClient? ticketChat = null, bool topicGuard = false)
    {
        var options = Options.Create(new AnswerProviderOptions { TopicGuardEnabled = topicGuard });
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

    // ── JSON response builders ──────────────────────────────────────────────────────

    private static string AnswerJson(string message) =>
        $$"""{"action":"answer","message":"{{message}}"}""";

    private static string EscalateJson(string message = "ok") =>
        $$"""{"action":"escalate","message":"{{message}}"}""";

    /// <summary>Streaming deltas for an answer whose message value is split into the given parts.</summary>
    private static string[] AnswerDeltas(params string[] messageParts) =>
        [.. new[] { "{\"action\":\"answer\",\"message\":\"" }.Concat(messageParts).Append("\"}")];

    private static async Task<List<BotEvent>> CollectAsync(IAsyncEnumerable<BotEvent> stream)
    {
        var events = new List<BotEvent>();
        await foreach (var e in stream)
            events.Add(e);
        return events;
    }

    private static string StreamedText(IEnumerable<BotEvent> events) =>
        string.Concat(events.Where(e => e.Kind == BotEventKind.TextChunk).Select(e => e.Text));

    // ── Non-streaming ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnswerAsync_Answer_ReturnsMessage()
    {
        var provider = CreateProvider(new FakeLlmChatClient(AnswerJson("Hello, how can I help?")));

        var result = await provider.AnswerAsync("hi", EmptySession());

        Assert.Equal(AnswerStatus.Answered, result.Status);
        Assert.Equal("Hello, how can I help?", result.Message);
        Assert.False(result.FailedOpen);
    }

    [Fact]
    public async Task AnswerAsync_Escalate_ReturnsNeedHuman()
    {
        var provider = CreateProvider(new FakeLlmChatClient(EscalateJson("This needs a human.")));

        var result = await provider.AnswerAsync("refund please", EmptySession());

        Assert.Equal(AnswerStatus.NeedHuman, result.Status);
    }

    [Fact]
    public async Task AnswerAsync_AllInvalid_EscalatesToHuman()
    {
        // Every attempt returns non-JSON → after MaxAnswerAttempts, escalate rather than show garbage.
        var provider = CreateProvider(new FakeLlmChatClient("not json at all"));

        var result = await provider.AnswerAsync("hi", EmptySession());

        Assert.Equal(AnswerStatus.NeedHuman, result.Status);
    }

    [Fact]
    public async Task AnswerAsync_RetriesPastInvalidThenAnswers()
    {
        // First response is broken (":"), second is valid → the valid answer is returned.
        var chat = new SequencedChatClient([":", AnswerJson("hello there")]);
        var provider = CreateProvider2(chat);

        var result = await provider.AnswerAsync("hi", EmptySession());

        Assert.Equal(AnswerStatus.Answered, result.Status);
        Assert.Equal("hello there", result.Message);
    }

    [Fact]
    public async Task AnswerAsync_RetriesPastWhitespaceMessageThenAnswers()
    {
        var chat = new SequencedChatClient([AnswerJson("       "), AnswerJson("real answer")]);
        var provider = CreateProvider2(chat);

        var result = await provider.AnswerAsync("hi", EmptySession());

        Assert.Equal(AnswerStatus.Answered, result.Status);
        Assert.Equal("real answer", result.Message);
    }

    [Fact]
    public async Task AnswerAsync_RawResponseRecordedOnResult()
    {
        var json = AnswerJson("hi");
        var provider = CreateProvider(new FakeLlmChatClient(json));

        var result = await provider.AnswerAsync("hi", EmptySession());

        Assert.Equal(json, result.RawResponse);
    }

    [Fact]
    public async Task AnswerAsync_OffTopic_ReturnsOffTopicResponse_WhenGuardEnabled()
    {
        var json = """{"offtopic":true,"action":"answer","message":""}""";
        var options = new AnswerProviderOptions { TopicGuardEnabled = true, OffTopicResponse = "Out of scope, sorry." };
        var provider = new AnswerProvider(
            new FakeLlmChatClient(json),
            new FakeDocumentLoader("doc"),
            new TicketGenerator(new FakeLlmChatClient("{}"), Options.Create(options), NullLogger<TicketGenerator>.Instance),
            [],
            new DefaultSystemPromptBuilder(Options.Create(options)),
            Options.Create(options),
            NullLogger<AnswerProvider>.Instance);

        var result = await provider.AnswerAsync("who won the football?", EmptySession());

        Assert.Equal(AnswerStatus.OffTopic, result.Status);
        Assert.Equal("Out of scope, sorry.", result.Message);
    }

    [Fact]
    public async Task AnswerAsync_OffTopicFlagIgnored_WhenGuardDisabled()
    {
        var json = """{"offtopic":true,"action":"answer","message":"actually answered"}""";
        var provider = CreateProvider(new FakeLlmChatClient(json), topicGuard: false);

        var result = await provider.AnswerAsync("hi", EmptySession());

        Assert.Equal(AnswerStatus.Answered, result.Status);
        Assert.Equal("actually answered", result.Message);
    }

    [Fact]
    public async Task AnswerAsync_SendsSendHistory_NotFullHistory()
    {
        var chat = new FakeLlmChatClient(AnswerJson("ok"));
        var provider = CreateProvider(chat);
        var session = new ConversationSession(
            FullHistory: [new(ChatRole.User, "FULL-ONLY-MARKER")],
            SendHistory: [new(ChatRole.User, "SEND-ONLY-MARKER")],
            LastActivity: DateTimeOffset.UtcNow);

        await provider.AnswerAsync("now", session);

        Assert.Contains(chat.LastMessages!, m => m.Content.Contains("SEND-ONLY-MARKER"));
        Assert.DoesNotContain(chat.LastMessages!, m => m.Content.Contains("FULL-ONLY-MARKER"));
    }

    // ── Streaming ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StreamAsync_Answer_StreamsMessageTokensOnly()
    {
        var chat = new FakeLlmChatClient("unused", AnswerDeltas("Hello", " world"));
        var provider = CreateProvider(chat);

        var events = await CollectAsync(provider.StreamAsync("hi", EmptySession()));

        Assert.DoesNotContain(events, e => (e.Text ?? "").Contains("action"));
        Assert.Equal("Hello world", StreamedText(events));
        Assert.Equal(BotEventKind.Done, events[^1].Kind);
        Assert.Equal("Hello world", events[^1].Result!.Message);
    }

    [Fact]
    public async Task StreamAsync_Escalate_EmitsEscalateAndCollectContactNoContent()
    {
        var chat = new FakeLlmChatClient("unused", [EscalateJson("internal reason")]);
        var provider = CreateProvider(chat);

        var events = await CollectAsync(provider.StreamAsync("refund", EmptySession()));

        Assert.Equal(3, events.Count);
        Assert.Equal(BotEventKind.Escalated, events[0].Kind);
        Assert.Equal(BotEventKind.Usage, events[1].Kind); // token accounting before the contact prompt
        Assert.Equal(BotEventKind.CollectContact, events[2].Kind);
        Assert.DoesNotContain(events, e => e.Kind == BotEventKind.TextChunk);
    }

    [Fact]
    public async Task StreamAsync_EscalateSplitAcrossDeltas_StillSuppressesContent()
    {
        var chat = new FakeLlmChatClient("unused", ["{\"action\":\"esc", "alate\",\"mess", "age\":\"leak\"}"]);
        var provider = CreateProvider(chat);

        var events = await CollectAsync(provider.StreamAsync("x", EmptySession()));

        Assert.Equal(3, events.Count);
        Assert.Equal(BotEventKind.Escalated, events[0].Kind);
        Assert.Equal(BotEventKind.Usage, events[1].Kind);
        Assert.Equal(BotEventKind.CollectContact, events[2].Kind);
    }

    [Fact]
    public async Task StreamAsync_AllInvalid_EscalatesToHuman()
    {
        // Non-JSON every attempt: nothing is streamed and the turn escalates (CollectContact, no answer).
        var chat = new FakeLlmChatClient("not json", ["abcdefghijklmnopqrstuvwxyz"]);
        var provider = CreateProvider(chat);

        var events = await CollectAsync(provider.StreamAsync("x", EmptySession()));

        Assert.DoesNotContain(events, e => e.Kind == BotEventKind.TextChunk);
        Assert.Contains(events, e => e.Kind == BotEventKind.Escalated);
        Assert.Contains(events, e => e.Kind == BotEventKind.CollectContact);
    }

    [Fact]
    public async Task StreamAsync_WhitespaceMessageThenRetryAnswers()
    {
        // Attempt 1 streams a whitespace-only message (nothing shown); the buffered retry answers.
        var chat = new SequencedChatClient([AnswerJson("    "), AnswerJson("real reply")]);
        var provider = CreateProvider2(chat);

        var events = await CollectAsync(provider.StreamAsync("x", EmptySession()));

        Assert.Equal("real reply", StreamedText(events));
        Assert.Equal(BotEventKind.Done, events[^1].Kind);
        Assert.Equal(AnswerStatus.Answered, events[^1].Result!.Status);
    }

    [Fact]
    public async Task StreamAsync_OffTopic_EmitsBlockedAndDone_WhenGuardEnabled()
    {
        var json = """{"offtopic":true,"action":"answer","message":"x"}""";
        var chat = new FakeLlmChatClient("unused", [json]);
        var provider = CreateProvider(chat, topicGuard: true);

        var events = await CollectAsync(provider.StreamAsync("off topic", EmptySession()));

        Assert.Contains(events, e => e.Kind == BotEventKind.Blocked);
        Assert.DoesNotContain(events, e => e.Kind == BotEventKind.TextChunk);
        Assert.Equal(BotEventKind.Done, events[^1].Kind);
        Assert.Equal(AnswerStatus.OffTopic, events[^1].Result!.Status);
    }

    // ── Escalation lifecycle ────────────────────────────────────────────────────────

    [Fact]
    public async Task StreamAsync_EscalationPendingWithContact_EmitsTicketConfirmedAndDone()
    {
        const string ticketJson = """{"summary":"Refund issue","details":"User wants refund","priority":"medium"}""";
        var ticketChat = new FakeLlmChatClient(ticketJson);
        var provider = CreateProvider(new FakeLlmChatClient("unused"), ticketChat);
        var session = new ConversationSession([], [], DateTimeOffset.UtcNow, EscalationPending: true, EscalationTriggerMessage: "I want a refund");
        var contact = new ContactInfo("a@b.com", null);

        var events = await CollectAsync(provider.StreamAsync("contact submitted", session, contact));

        Assert.Equal(3, events.Count);
        Assert.Equal(BotEventKind.Usage, events[0].Kind); // ticket-summarisation tokens
        Assert.Equal(BotEventKind.TicketConfirmed, events[1].Kind);
        Assert.Equal(BotEventKind.Done, events[2].Kind);
        Assert.Equal(AnswerStatus.TicketCreated, events[2].Result!.Status);
        Assert.NotNull(events[1].TicketId);
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
        var chat = new FakeLlmChatClient("unused", AnswerDeltas("Here is your answer."));
        var provider = CreateProvider(chat);
        var session = new ConversationSession([], [], DateTimeOffset.UtcNow, EscalationPending: true);

        var events = await CollectAsync(provider.StreamAsync("hello", session, contact: null));

        Assert.Equal(BotEventKind.Done, events[^1].Kind);
        Assert.Contains(events, e => e.Kind == BotEventKind.TextChunk);
    }

    // ── Escalation flow ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task StreamAsync_EscalateDetected_EmitsCollectContactWhenNoContact()
    {
        var chat = new FakeLlmChatClient("unused", [EscalateJson("speak to a human")]);
        var provider = CreateProvider(chat);
        var events = await CollectAsync(provider.StreamAsync("issue", EmptySession(), contact: null));

        Assert.Contains(events, e => e.Kind == BotEventKind.Escalated);
        Assert.Contains(events, e => e.Kind == BotEventKind.CollectContact);
        Assert.DoesNotContain(events, e => e.Kind == BotEventKind.TextChunk);
    }

    [Fact]
    public async Task AnswerAsync_EscalateDetected_NoContactReturnsNeedHuman()
    {
        var chat = new FakeLlmChatClient(EscalateJson("Connect with support."));
        var provider = CreateProvider(chat);
        var result = await provider.AnswerAsync("problem", EmptySession(), contact: null);

        Assert.Equal(AnswerStatus.NeedHuman, result.Status);
    }

    [Fact]
    public async Task StreamAsync_EscalateWithContactProvided_GeneratesTicket()
    {
        var chat = new FakeLlmChatClient("unused", [EscalateJson("connecting you")]);
        var ticketChat = new FakeLlmChatClient("""{"summary":"Broken feature","details":"User reports issue","priority":"medium"}""");
        var provider = CreateProvider(chat, ticketChat);
        var contact = new ContactInfo("user@example.com", null, "John", "john99");

        var events = await CollectAsync(provider.StreamAsync("urgent problem", EmptySession(), contact));

        Assert.Equal(BotEventKind.Escalated, events[0].Kind);
        Assert.Contains(events, e => e.Kind == BotEventKind.TicketConfirmed);
        Assert.DoesNotContain(events, e => e.Kind == BotEventKind.CollectContact);
    }

    [Fact]
    public async Task AnswerAsync_EscalateResponse_ReturnsNeedHumanNotTicket()
    {
        var chat = new FakeLlmChatClient(EscalateJson("I'll help."));
        var provider = CreateProvider(chat);
        var contact = new ContactInfo("test@example.com", null, "Alice", null);

        var result = await provider.AnswerAsync("cant login", EmptySession(), contact);

        Assert.Equal(AnswerStatus.NeedHuman, result.Status);
    }

    [Fact]
    public async Task StreamAsync_EscalateWithNullContactEmail_StillGeneratesTicket()
    {
        var chat = new FakeLlmChatClient("unused", [EscalateJson("connecting you")]);
        var ticketChat = new FakeLlmChatClient("""{"summary":"Support needed","details":"Issue reported","priority":"medium"}""");
        var provider = CreateProvider(chat, ticketChat);
        var contact = new ContactInfo(null, null, "Jane");

        var events = await CollectAsync(provider.StreamAsync("issue", EmptySession(), contact));

        Assert.Contains(events, e => e.Kind == BotEventKind.TicketConfirmed);
        Assert.DoesNotContain(events, e => e.Kind == BotEventKind.CollectContact);
    }

    // ── Sequenced fake (returns a different response per call, for retry tests) ──────

    private static AnswerProvider CreateProvider2(ILlmChatClient chat)
    {
        var options = Options.Create(new AnswerProviderOptions());
        return new(
            chat,
            new FakeDocumentLoader("doc content"),
            new TicketGenerator(chat, options, NullLogger<TicketGenerator>.Instance),
            [],
            new DefaultSystemPromptBuilder(options),
            options,
            NullLogger<AnswerProvider>.Instance);
    }

    private sealed class SequencedChatClient(IReadOnlyList<string> responses) : ILlmChatClient
    {
        private int _index;

        public string Name => "sequenced";

        private string Next() => responses[Math.Min(_index++, responses.Count - 1)];

        public Task<LlmChatResult> ChatAsync(
            IReadOnlyList<ChatMessage> messages, bool jsonObject = false, CancellationToken ct = default)
            => Task.FromResult(new LlmChatResult(Next()));

        public async IAsyncEnumerable<string> ChatStreamingAsync(
            IReadOnlyList<ChatMessage> messages, bool jsonObject = false, Action<int>? onUsage = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return Next();
            onUsage?.Invoke(0);
            await Task.CompletedTask;
        }
    }
}
