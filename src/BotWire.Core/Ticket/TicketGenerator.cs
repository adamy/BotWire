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

using System.Text.Json;
using BotWire.Core.Abstractions;
using BotWire.Core.Enums;
using BotWire.Core.Models;
using BotWire.Core.Rag;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotWire.Core.Ticket;

/// <summary>
/// Creates a <see cref="SupportTicket"/> by asking the LLM to compress the conversation into a
/// structured JSON summary. Contact details are applied after the LLM call so they are never
/// sent to the model.
/// </summary>
internal sealed class TicketGenerator
{
    private readonly ILlmChatClient _chat;
    private readonly ILogger<TicketGenerator> _logger;
    private readonly string _ticketPrefix;
    private readonly string _systemPrompt;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="chat">The LLM used to summarise the conversation.</param>
    /// <param name="options">Provider options supplying the ticket ID prefix and language.</param>
    /// <param name="logger">Logger for fallback diagnostics.</param>
    public TicketGenerator(ILlmChatClient chat, IOptions<AnswerProviderOptions> options, ILogger<TicketGenerator> logger)
    {
        _chat = chat;
        _ticketPrefix = options.Value.TicketPrefix;
        _systemPrompt = BuildSystemPrompt(options.Value.TicketLanguage);
        _logger = logger;
    }

    private static string BuildSystemPrompt(string ticketLanguage) =>
        "You are summarising a customer-support conversation so a human agent can pick it up.\n" +
        $"Write the 'summary' and 'details' values in {ticketLanguage}, regardless of the language the customer used.\n" +
        "Focus on what the customer needs — do not mention 'reference documents', 'knowledge base', or any bot internals.\n" +
        "Respond with ONLY a JSON object — no markdown, no explanation:\n" +
        "{\n" +
        "  \"summary\": \"<one-sentence summary of the customer's issue>\",\n" +
        "  \"details\": \"<full description including relevant context from the conversation>\",\n" +
        "  \"priority\": \"<low|medium|high|urgent>\"\n" +
        "}";

    /// <summary>
    /// Generates a <see cref="SupportTicket"/> by summarising the conversation via the LLM.
    /// <paramref name="contact"/> is applied to the ticket but never sent to the model.
    /// On JSON parse failure the raw LLM response is used as the summary (fail-open).
    /// </summary>
    /// <param name="session">The current conversation session.</param>
    /// <param name="triggerMessage">The user message that triggered the escalation.</param>
    /// <param name="contact">Optional user contact details; not passed to the LLM.</param>
    /// <param name="cancellationToken">Token to cancel the LLM call.</param>
    /// <returns>The generated ticket and the tokens billed for the summarisation call.</returns>
    public async Task<(SupportTicket Ticket, int Tokens)> GenerateAsync(
        ConversationSession session,
        string triggerMessage,
        ContactInfo? contact,
        CancellationToken cancellationToken = default)
    {
        var messages = BuildMessages(session);
        var completion = await _chat.ChatAsync(messages, jsonObject: false, cancellationToken);
        var tokens = completion.TotalTokens;
        var json = StripMarkdownFences(completion.Text);

        string summary, details;
        TicketPriority priority;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? string.Empty : string.Empty;
            details = root.TryGetProperty("details", out var d) ? d.GetString() ?? string.Empty : string.Empty;
            priority = root.TryGetProperty("priority", out var p) ? ParsePriority(p.GetString()) : TicketPriority.Medium;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BotWire: ticket JSON parse failed; using trigger message as fallback summary.");
            summary = triggerMessage;
            details = string.Empty;
            priority = TicketPriority.Medium;
        }

        var ticket = new SupportTicket(
            TicketId: TicketIdGenerator.Next(_ticketPrefix),
            UserMessage: triggerMessage,
            AiSummary: summary,
            Details: details,
            SuggestedPriority: priority,
            Contact: contact,
            History: session.FullHistory.AsReadOnly(),
            CreatedAt: DateTimeOffset.UtcNow);

        return (ticket, tokens);
    }

    private List<ChatMessage> BuildMessages(ConversationSession session)
    {
        var messages = new List<ChatMessage>(session.FullHistory.Count + 2)
        {
            new(ChatRole.System, _systemPrompt),
        };

        foreach (var msg in session.FullHistory)
        {
            if (msg.Role != ChatRole.System)
                messages.Add(msg);
        }

        // Explicit instruction prevents the LLM from continuing the conversation instead of summarising.
        messages.Add(new(ChatRole.User, "This conversation has ended. Generate the JSON summary now."));
        return messages;
    }

    private static string StripMarkdownFences(string raw)
    {
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;
        var nl = trimmed.IndexOf('\n');
        if (nl < 0) return trimmed;
        var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return end > nl ? trimmed[(nl + 1)..end].Trim() : trimmed;
    }

    private static TicketPriority ParsePriority(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "low" => TicketPriority.Low,
        "high" => TicketPriority.High,
        "urgent" => TicketPriority.Urgent,
        _ => TicketPriority.Medium,
    };
}
