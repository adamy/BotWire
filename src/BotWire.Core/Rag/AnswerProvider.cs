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
using System.Text;
using System.Text.Json;
using BotWire.Core.Abstractions;
using BotWire.Core.Enums;
using BotWire.Core.Models;
using BotWire.Core.Ticket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotWire.Core.Rag;

/// <summary>
/// RAG Mode A answer provider: grounds an <see cref="ILlmChatClient"/> in a fixed document set and
/// requires a JSON-object reply (<c>{offtopic, action, message}</c>) to decide whether the bot
/// answered, classified the message off-topic, or must hand off to a human. Empty/invalid replies
/// are retried up to <see cref="AnswerProviderOptions.MaxAnswerAttempts"/> times before escalating.
/// The system prompt is assembled (and its token budget enforced) on first use.
/// </summary>
public sealed class AnswerProvider : IAnswerProvider
{
    private readonly ILlmChatClient _chat;
    private readonly IDocumentLoader _loader;
    private readonly TicketGenerator _ticketGenerator;
    private readonly IReadOnlyList<INotificationChannel> _channels;
    private readonly ISystemPromptBuilder _promptBuilder;
    private readonly AnswerProviderOptions _options;
    private readonly ILogger<AnswerProvider> _logger;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private string? _systemPrompt;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="chat">The chat LLM used to generate answers.</param>
    /// <param name="loader">Loader for the knowledge-base documents.</param>
    /// <param name="ticketGenerator">Generator for escalation support tickets.</param>
    /// <param name="channels">Zero or more notification channels. Empty when email is not configured.</param>
    /// <param name="promptBuilder">Builder for the grounding system prompt.</param>
    /// <param name="options">Bound provider options (document paths, preamble).</param>
    /// <param name="logger">Logger for fail-open diagnostics.</param>
    internal AnswerProvider(
        ILlmChatClient chat,
        IDocumentLoader loader,
        TicketGenerator ticketGenerator,
        IEnumerable<INotificationChannel> channels,
        ISystemPromptBuilder promptBuilder,
        IOptions<AnswerProviderOptions> options,
        ILogger<AnswerProvider> logger)
    {
        _chat = chat;
        _loader = loader;
        _ticketGenerator = ticketGenerator;
        _channels = channels.ToList();
        _promptBuilder = promptBuilder;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AnswerResult> AnswerAsync(
        string message,
        ConversationSession session,
        ContactInfo? contact = null,
        CancellationToken cancellationToken = default)
    {
        if (session.EscalationPending && contact is not null)
        {
            if (session.EscalationTriggerMessage is null)
                _logger.LogWarning("BotWire: EscalationPending is true but EscalationTriggerMessage is null; falling back to current message as ticket UserMessage.");
            var (ticket, ticketTokens) = await _ticketGenerator.GenerateAsync(
                session, session.EscalationTriggerMessage ?? message, contact, cancellationToken);
            await NotifyAsync(ticket, cancellationToken);
            return new AnswerResult(AnswerStatus.TicketCreated, ticket.TicketId, TokensUsed: ticketTokens);
        }

        var systemPrompt = await GetSystemPromptAsync(cancellationToken);
        var messages = BuildMessages(systemPrompt, session, message);

        // Retry empty/invalid responses a few times before handing off to a human, since a single
        // garbled reply (bad JSON, or a whitespace-only message) should not surface as a blank answer.
        var attempts = Math.Max(1, _options.MaxAnswerAttempts);
        var tokens = 0;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var completion = await _chat.ChatAsync(messages, jsonObject: true, cancellationToken);
            tokens += completion.TotalTokens;
            var raw = completion.Text;
            var parsed = ParseAnswerJson(raw);

            if (parsed.Ok && parsed.OffTopic && _options.TopicGuardEnabled)
                return new AnswerResult(AnswerStatus.OffTopic, _options.OffTopicResponse, RawResponse: raw, TokensUsed: tokens);

            if (parsed.Ok && parsed.Action == "escalate")
                return new AnswerResult(AnswerStatus.NeedHuman, _options.AutoEscalationMessage, RawResponse: raw, TokensUsed: tokens);

            if (parsed.Ok && !string.IsNullOrWhiteSpace(parsed.Message))
                return new AnswerResult(AnswerStatus.Answered, parsed.Message, RawResponse: raw, TokensUsed: tokens);

            _logger.LogWarning(
                "BotWire: empty or invalid answer response (attempt {Attempt}/{Max}): '{Raw}'",
                attempt, attempts, Truncate(raw));
        }

        // Every attempt was empty or invalid — escalate to a human rather than show a blank answer.
        _logger.LogWarning("BotWire: {Max} answer attempts were all empty/invalid — escalating to a human.", attempts);
        return new AnswerResult(AnswerStatus.NeedHuman, _options.AutoEscalationMessage, TokensUsed: tokens);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<BotEvent> StreamAsync(
        string message,
        ConversationSession session,
        ContactInfo? contact = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (session.EscalationPending && contact is not null)
        {
            if (session.EscalationTriggerMessage is null)
                _logger.LogWarning("BotWire: EscalationPending is true but EscalationTriggerMessage is null; falling back to current message as ticket UserMessage.");
            var (ticket, ticketTokens) = await _ticketGenerator.GenerateAsync(
                session, session.EscalationTriggerMessage ?? message, contact, cancellationToken);
            await NotifyAsync(ticket, cancellationToken);
            var confirmMsg = _options.TicketConfirmedMessage.Replace("{ticketId}", ticket.TicketId);
            yield return BotEvent.Usage(ticketTokens);
            yield return BotEvent.TicketConfirmed(ticket.TicketId, confirmMsg);
            yield return BotEvent.Done(new AnswerResult(AnswerStatus.TicketCreated, ticket.TicketId));
            yield break;
        }

        var systemPrompt = await GetSystemPromptAsync(cancellationToken);
        var messages = BuildMessages(systemPrompt, session, message);

        // Running token total for the turn; captured by EmitEscalation below and surfaced via Usage
        // events so escalation paths (which emit no Done) still report usage.
        var tokens = 0;

        // Reused by both the live first attempt and the buffered retries below.
        async IAsyncEnumerable<BotEvent> EmitEscalation(bool announce)
        {
            if (announce)
                yield return BotEvent.Escalated();
            if (contact is not null)
            {
                var (ticket, ticketTokens) = await _ticketGenerator.GenerateAsync(session, message, contact, cancellationToken);
                await NotifyAsync(ticket, cancellationToken);
                var confirmMsg = _options.TicketConfirmedMessage.Replace("{ticketId}", ticket.TicketId);
                // Report the escalate-decision tokens plus the ticket-summarisation tokens.
                yield return BotEvent.Usage(tokens + ticketTokens);
                yield return BotEvent.TicketConfirmed(ticket.TicketId, confirmMsg);
                yield return BotEvent.Done(new AnswerResult(AnswerStatus.TicketCreated, ticket.TicketId));
            }
            else
            {
                // No contact yet: surface the escalate-decision tokens before the contact prompt,
                // which has no Done event to carry them.
                yield return BotEvent.Usage(tokens);
                yield return BotEvent.CollectContact();
            }
        }

        // ── Attempt 1: stream the answer live ──
        var reader = new JsonAnswerStreamReader(_options.TopicGuardEnabled);
        var answer = new StringBuilder();
        var mode   = StreamMode.Pending;
        var emitted = false; // true once any non-whitespace message text has been streamed

        await foreach (var delta in _chat.ChatStreamingAsync(messages, jsonObject: true, onUsage: t => tokens += t, cancellationToken))
        {
            foreach (var output in reader.Feed(delta))
            {
                if (output.Kind == JsonAnswerStreamReader.OutputKind.PreludeResolved)
                {
                    if (reader.OffTopic && _options.TopicGuardEnabled)
                    {
                        _logger.LogDebug("BotWire: message classified off-topic.");
                        mode = StreamMode.OffTopic;
                        yield return BotEvent.Blocked(_options.OffTopicResponse);
                    }
                    else if (reader.Action == "escalate")
                    {
                        _logger.LogDebug("BotWire: action=escalate.");
                        mode = StreamMode.Escalate;
                        yield return BotEvent.Escalated();
                    }
                    else
                    {
                        mode = StreamMode.Answer;
                    }
                }
                else if (output.Kind == JsonAnswerStreamReader.OutputKind.MessageDelta && mode == StreamMode.Answer)
                {
                    answer.Append(output.Text);
                    // Hold leading whitespace so a whitespace-only message streams nothing and can be retried.
                    if (!emitted)
                    {
                        if (string.IsNullOrWhiteSpace(answer.ToString())) continue;
                        emitted = true;
                        yield return BotEvent.TextChunk(answer.ToString().TrimStart());
                    }
                    else
                    {
                        yield return BotEvent.TextChunk(output.Text!);
                    }
                }
            }

            if (reader.Failed)
                break;
        }
        reader.Finish();

        // Good outcomes from attempt 1.
        if (mode == StreamMode.Escalate)
        {
            await foreach (var e in EmitEscalation(announce: false)) yield return e;
            yield break;
        }
        if (mode == StreamMode.OffTopic)
        {
            yield return BotEvent.Done(new AnswerResult(AnswerStatus.OffTopic, _options.OffTopicResponse, RawResponse: reader.Raw, TokensUsed: tokens));
            yield break;
        }
        if (mode == StreamMode.Answer && emitted)
        {
            yield return BotEvent.Done(new AnswerResult(AnswerStatus.Answered, answer.ToString().Trim(), RawResponse: reader.Raw, TokensUsed: tokens));
            yield break;
        }

        // ── Attempt 1 produced nothing usable (invalid JSON, or empty/whitespace message). ──
        // Nothing user-visible was streamed, so retry (buffered) before handing off to a human.
        _logger.LogWarning("BotWire: streamed answer was empty/invalid: '{Raw}'", Truncate(reader.Raw));
        var attempts = Math.Max(1, _options.MaxAnswerAttempts);
        for (var attempt = 2; attempt <= attempts; attempt++)
        {
            var completion = await _chat.ChatAsync(messages, jsonObject: true, cancellationToken);
            tokens += completion.TotalTokens;
            var raw = completion.Text;
            var parsed = ParseAnswerJson(raw);

            if (parsed.Ok && parsed.OffTopic && _options.TopicGuardEnabled)
            {
                yield return BotEvent.Blocked(_options.OffTopicResponse);
                yield return BotEvent.Done(new AnswerResult(AnswerStatus.OffTopic, _options.OffTopicResponse, RawResponse: raw, TokensUsed: tokens));
                yield break;
            }
            if (parsed.Ok && parsed.Action == "escalate")
            {
                await foreach (var e in EmitEscalation(announce: true)) yield return e;
                yield break;
            }
            if (parsed.Ok && !string.IsNullOrWhiteSpace(parsed.Message))
            {
                yield return BotEvent.TextChunk(parsed.Message);
                yield return BotEvent.Done(new AnswerResult(AnswerStatus.Answered, parsed.Message, RawResponse: raw, TokensUsed: tokens));
                yield break;
            }

            _logger.LogWarning(
                "BotWire: retry answer empty/invalid (attempt {Attempt}/{Max}): '{Raw}'", attempt, attempts, Truncate(raw));
        }

        // Every attempt was empty or invalid — escalate to a human.
        _logger.LogWarning("BotWire: {Max} answer attempts were all empty/invalid — escalating to a human.", attempts);
        await foreach (var e in EmitEscalation(announce: true)) yield return e;
    }

    private enum StreamMode { Pending, Answer, Escalate, OffTopic }

    private async Task NotifyAsync(SupportTicket ticket, CancellationToken ct)
    {
        foreach (var channel in _channels)
        {
            try { await channel.SendTicketAsync(ticket, ct); }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "BotWire: notification channel '{Type}' failed for ticket {Id}.",
                    channel.ChannelType, ticket.TicketId);
            }
        }

        if (_options.OnTicketCreated is not null)
        {
            try { await _options.OnTicketCreated(ticket); }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "BotWire: OnTicketCreated callback threw for ticket {Id}.", ticket.TicketId);
            }
        }
    }

    private static string QuoteUserMessage(string content) =>
        $"{{\"user_message\": {JsonSerializer.Serialize(content)}}}";

    /// <summary>Shortens a raw response for log lines so a long reply does not flood the log.</summary>
    private static string Truncate(string s) =>
        s.Length <= 200 ? s : s[..200] + "…";

    /// <summary>
    /// Parses the answer JSON <c>{offtopic, action, message}</c>. Returns <c>Ok = false</c> when the
    /// text is not a JSON object, so the caller can fall back to treating it as a plain answer.
    /// </summary>
    private static (bool Ok, bool OffTopic, string Action, string Message) ParseAnswerJson(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return (false, false, "answer", "");

            var offtopic = root.TryGetProperty("offtopic", out var o) && o.ValueKind == JsonValueKind.True;
            var action = root.TryGetProperty("action", out var a) && a.ValueKind == JsonValueKind.String
                ? (a.GetString() ?? "answer").Trim().ToLowerInvariant()
                : "answer";
            var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString() ?? ""
                : "";
            return (true, offtopic, action, message);
        }
        catch (JsonException)
        {
            return (false, false, "answer", "");
        }
    }

    private static List<ChatMessage> BuildMessages(
        string systemPrompt,
        ConversationSession session,
        string userMessage)
    {
        var messages = new List<ChatMessage>(session.SendHistory.Count + 3)
        {
            new(ChatRole.System, systemPrompt),
        };
        // Quote every user message so the LLM treats user content as data, not instructions.
        foreach (var m in session.SendHistory)
            messages.Add(m.Role == ChatRole.User ? new(ChatRole.User, QuoteUserMessage(m.Content)) : m);
        // Injected just before the user turn — escalates to a critical warning if recent turns
        // were not valid JSON, since the model is clearly drifting.
        var reminder = session.ConsecutiveNoControlWordCount >= 3
            ? $"CRITICAL ERROR: Your last {session.ConsecutiveNoControlWordCount} replies were NOT the required JSON object. " +
              "The application is broken. You MUST reply with ONLY the JSON object described in the output format — " +
              "no markdown, no code fence, no text before or after it, absolutely no exceptions."
            : session.ConsecutiveNoControlWordCount > 0
            ? "CRITICAL ERROR: Your previous reply was not the required JSON object. " +
              "The application broke. You MUST reply with ONLY the JSON object described in the output format — " +
              "no markdown, no text outside it, no exceptions."
            : "REMINDER: Reply with ONLY the JSON object described in the output format — no markdown, nothing outside it.";
        messages.Add(new(ChatRole.System, reminder));
        messages.Add(new(ChatRole.User, QuoteUserMessage(userMessage)));
        return messages;
    }

    private async Task<string> GetSystemPromptAsync(CancellationToken cancellationToken)
    {
        if (_systemPrompt is not null)
            return _systemPrompt;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_systemPrompt is null)
            {
                var documents = await _loader.LoadAsync([.. _options.DocumentPaths], cancellationToken);
                _systemPrompt = _promptBuilder.Build(documents);
            }
        }
        finally
        {
            _initLock.Release();
        }

        return _systemPrompt;
    }
}
