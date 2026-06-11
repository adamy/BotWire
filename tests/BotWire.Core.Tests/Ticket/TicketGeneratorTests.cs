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
using BotWire.Core.Enums;
using BotWire.Core.Models;
using BotWire.Core.Rag;
using BotWire.Core.Ticket;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BotWire.Core.Tests.Ticket;

public class TicketGeneratorTests
{
    private static ConversationSession EmptySession() => new([], [], DateTimeOffset.UtcNow);

    private static TicketGenerator CreateGenerator(ILlmChatClient chat, string prefix = "TKT", string language = "English") =>
        new(chat, Options.Create(new AnswerProviderOptions { TicketPrefix = prefix, TicketLanguage = language }), NullLogger<TicketGenerator>.Instance);

    [Fact]
    public async Task GenerateAsync_ValidJson_ParsesAllFields()
    {
        const string json = """{"summary":"Login broken","details":"User cannot log in","priority":"high"}""";
        var gen = CreateGenerator(new FakeChat(json));

        var ticket = await gen.GenerateAsync(EmptySession(), "I can't log in", null);

        Assert.Equal("Login broken", ticket.AiSummary);
        Assert.Equal("User cannot log in", ticket.Details);
        Assert.Equal(TicketPriority.High, ticket.SuggestedPriority);
        Assert.Equal("I can't log in", ticket.UserMessage);
    }

    [Fact]
    public async Task GenerateAsync_InvalidJson_FallbackToTriggerMessage()
    {
        var gen = CreateGenerator(new FakeChat("I'm sorry to hear that. Let me help you."));

        var ticket = await gen.GenerateAsync(EmptySession(), "My order has a faulty product", null);

        Assert.Equal("My order has a faulty product", ticket.AiSummary);
        Assert.Equal(string.Empty, ticket.Details);
        Assert.Equal(TicketPriority.Medium, ticket.SuggestedPriority);
    }

    [Fact]
    public async Task GenerateAsync_MarkdownFencedJson_Parsed()
    {
        const string fenced = "```json\n{\"summary\":\"Faulty product\",\"details\":\"Order has damage\",\"priority\":\"high\"}\n```";
        var ticket = await CreateGenerator(new FakeChat(fenced)).GenerateAsync(EmptySession(), "trigger", null);

        Assert.Equal("Faulty product", ticket.AiSummary);
        Assert.Equal("Order has damage", ticket.Details);
        Assert.Equal(TicketPriority.High, ticket.SuggestedPriority);
    }

    [Fact]
    public async Task GenerateAsync_FinalInstructionMessage_AppendedLast()
    {
        const string json = """{"summary":"s","details":"d","priority":"low"}""";
        var capture = new CapturingChat(json);
        var gen = CreateGenerator(capture);

        await gen.GenerateAsync(EmptySession(), "trigger", null);

        var last = capture.LastMessages!.Last();
        Assert.Equal(ChatRole.User, last.Role);
        Assert.Contains("Generate the JSON summary", last.Content);
    }

    [Fact]
    public async Task GenerateAsync_ContactNotPassedToLlm()
    {
        const string json = """{"summary":"s","details":"d","priority":"low"}""";
        var capture = new CapturingChat(json);
        var gen = CreateGenerator(capture);
        var contact = new ContactInfo("user@example.com", "555-1234");

        await gen.GenerateAsync(EmptySession(), "trigger", contact);

        Assert.NotNull(capture.LastMessages);
        var allContent = string.Concat(capture.LastMessages!.Select(m => m.Content));
        Assert.DoesNotContain("user@example.com", allContent);
        Assert.DoesNotContain("555-1234", allContent);
    }

    [Fact]
    public async Task GenerateAsync_ContactAppliedToTicket()
    {
        const string json = """{"summary":"s","details":"d","priority":"low"}""";
        var contact = new ContactInfo("user@example.com", null);
        var gen = CreateGenerator(new FakeChat(json));

        var ticket = await gen.GenerateAsync(EmptySession(), "trigger", contact);

        Assert.Equal(contact, ticket.Contact);
    }

    [Fact]
    public async Task GenerateAsync_SystemMessagesInHistory_ExcludedFromLlmMessages()
    {
        const string json = """{"summary":"s","details":"d","priority":"medium"}""";
        var capture = new CapturingChat(json);
        var gen = CreateGenerator(capture);
        var session = new ConversationSession(
            FullHistory:
            [
                new ChatMessage(ChatRole.System, "old system prompt"),
                new ChatMessage(ChatRole.User, "hello"),
            ],
            SendHistory: [],
            LastActivity: DateTimeOffset.UtcNow);

        await gen.GenerateAsync(session, "trigger", null);

        Assert.NotNull(capture.LastMessages);
        // first message is TicketGenerator's own system prompt; no other system messages
        var systemMessages = capture.LastMessages!.Where(m => m.Role == ChatRole.System).ToList();
        Assert.Single(systemMessages);
        Assert.DoesNotContain("old system prompt", systemMessages[0].Content);
        // user message from history should be forwarded
        Assert.Contains(capture.LastMessages!, m => m.Role == ChatRole.User && m.Content == "hello");
    }

    [Fact]
    public async Task GenerateAsync_PriorityUrgent_Parsed()
    {
        const string json = """{"summary":"s","details":"d","priority":"urgent"}""";
        var ticket = await CreateGenerator(new FakeChat(json)).GenerateAsync(EmptySession(), "t", null);
        Assert.Equal(TicketPriority.Urgent, ticket.SuggestedPriority);
    }

    [Fact]
    public async Task GenerateAsync_PriorityLow_Parsed()
    {
        const string json = """{"summary":"s","details":"d","priority":"low"}""";
        var ticket = await CreateGenerator(new FakeChat(json)).GenerateAsync(EmptySession(), "t", null);
        Assert.Equal(TicketPriority.Low, ticket.SuggestedPriority);
    }

    [Fact]
    public async Task GenerateAsync_UnknownPriority_DefaultsMedium()
    {
        const string json = """{"summary":"s","details":"d","priority":"critical"}""";
        var ticket = await CreateGenerator(new FakeChat(json)).GenerateAsync(EmptySession(), "t", null);
        Assert.Equal(TicketPriority.Medium, ticket.SuggestedPriority);
    }

    [Fact]
    public async Task GenerateAsync_CustomPrefix_UsedInTicketId()
    {
        const string json = """{"summary":"s","details":"d","priority":"low"}""";
        var ticket = await CreateGenerator(new FakeChat(json), prefix: "ACME").GenerateAsync(EmptySession(), "t", null);
        Assert.StartsWith("ACME-", ticket.TicketId);
    }

    [Fact]
    public async Task GenerateAsync_TicketLanguage_InjectedIntoSystemPrompt()
    {
        const string json = """{"summary":"s","details":"d","priority":"low"}""";
        var capture = new CapturingChat(json);
        var gen = CreateGenerator(capture, language: "简体中文");

        await gen.GenerateAsync(EmptySession(), "trigger", null);

        var system = capture.LastMessages!.Single(m => m.Role == ChatRole.System).Content;
        Assert.Contains("简体中文", system);
    }

    [Fact]
    public async Task GenerateAsync_DefaultLanguage_IsEnglish()
    {
        const string json = """{"summary":"s","details":"d","priority":"low"}""";
        var capture = new CapturingChat(json);
        var gen = CreateGenerator(capture);

        await gen.GenerateAsync(EmptySession(), "trigger", null);

        var system = capture.LastMessages!.Single(m => m.Role == ChatRole.System).Content;
        Assert.Contains("English", system);
    }

    // ----- Test doubles -----

    private sealed class FakeChat(string response) : ILlmChatClient
    {
        public string Name => "fake";
        public Task<string> ChatAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default) =>
            Task.FromResult(response);
        public async IAsyncEnumerable<string> ChatStreamingAsync(IReadOnlyList<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken ct = default)
        { yield return response; await Task.Yield(); }
    }

    private sealed class CapturingChat(string response) : ILlmChatClient
    {
        public string Name => "capturing";
        public IReadOnlyList<ChatMessage>? LastMessages { get; private set; }
        public Task<string> ChatAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default)
        {
            LastMessages = messages;
            return Task.FromResult(response);
        }
        public async IAsyncEnumerable<string> ChatStreamingAsync(IReadOnlyList<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken ct = default)
        { yield return response; await Task.Yield(); }
    }
}
