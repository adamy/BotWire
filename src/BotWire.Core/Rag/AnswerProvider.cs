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
/// uses a first-line ANSWER / ESCALATE control word to decide whether the bot answered or must hand
/// off to a human. The system prompt is assembled (and its token budget enforced) on first use.
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
            var ticket = await _ticketGenerator.GenerateAsync(
                session, session.EscalationTriggerMessage ?? message, contact, cancellationToken);
            await NotifyAsync(ticket, cancellationToken);
            return new AnswerResult(AnswerStatus.TicketCreated, ticket.TicketId);
        }

        var triageContinued = false;
        if (session.ConsecutiveNoControlWordCount >= _options.FailOpenEscalateThreshold)
        {
            var needsHuman = await TriageEscalationAsync(session, message, cancellationToken);
            if (needsHuman)
            {
                _logger.LogInformation(
                    "BotWire: auto-triage after {Count} fail-open turns — escalating.",
                    session.ConsecutiveNoControlWordCount);
                return new AnswerResult(AnswerStatus.NeedHuman, _options.AutoEscalationMessage, FailedOpen: false);
            }
            _logger.LogInformation(
                "BotWire: auto-triage after {Count} fail-open turns — no escalation needed, resetting counter and continuing.",
                session.ConsecutiveNoControlWordCount);
            triageContinued = true;
        }

        var systemPrompt = await GetSystemPromptAsync(cancellationToken);
        var messages = BuildMessages(systemPrompt, session, message);

        var raw = await _chat.ChatAsync(messages, cancellationToken);
        var parsed = ResponseControl.Parse(raw);

        if (!parsed.Recognized)
            _logger.LogWarning("LLM response had no recognized control word; failing open as ANSWER.");

        // When triage just adjudicated the fail-open streak and chose to continue, reset the counter
        // (FailedOpen = false) so triage does not re-run on every subsequent turn.
        return new AnswerResult(parsed.Status, parsed.Message, FailedOpen: !parsed.Recognized && !triageContinued);
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
            var ticket = await _ticketGenerator.GenerateAsync(
                session, session.EscalationTriggerMessage ?? message, contact, cancellationToken);
            await NotifyAsync(ticket, cancellationToken);
            var confirmMsg = _options.TicketConfirmedMessage.Replace("{ticketId}", ticket.TicketId);
            yield return BotEvent.TicketConfirmed(ticket.TicketId, confirmMsg);
            yield return BotEvent.Done(new AnswerResult(AnswerStatus.TicketCreated, ticket.TicketId));
            yield break;
        }

        var triageContinued = false;
        if (session.ConsecutiveNoControlWordCount >= _options.FailOpenEscalateThreshold)
        {
            var needsHuman = await TriageEscalationAsync(session, message, cancellationToken);
            if (needsHuman)
            {
                _logger.LogInformation(
                    "BotWire: auto-triage after {Count} fail-open turns — escalating.",
                    session.ConsecutiveNoControlWordCount);
                var autoMsg = _options.AutoEscalationMessage;
                if (!string.IsNullOrEmpty(autoMsg))
                    yield return BotEvent.TextChunk(autoMsg);
                if (contact is not null)
                {
                    var ticket = await _ticketGenerator.GenerateAsync(
                        session, message, contact, cancellationToken);
                    await NotifyAsync(ticket, cancellationToken);
                    var confirmMsg = _options.TicketConfirmedMessage.Replace("{ticketId}", ticket.TicketId);
                    yield return BotEvent.TicketConfirmed(ticket.TicketId, confirmMsg);
                    yield return BotEvent.Done(new AnswerResult(AnswerStatus.TicketCreated, ticket.TicketId));
                }
                else
                {
                    yield return BotEvent.CollectContact();
                }
                yield break;
            }
            _logger.LogInformation(
                "BotWire: auto-triage after {Count} fail-open turns — no escalation needed, resetting counter and continuing.",
                session.ConsecutiveNoControlWordCount);
            triageContinued = true;
        }

        var systemPrompt = await GetSystemPromptAsync(cancellationToken);
        var messages = BuildMessages(systemPrompt, session, message);

        var buffer      = new StringBuilder();
        var answer      = new StringBuilder();
        var resolved    = false; // once true, the control-word decision is made and deltas pass through
        var escalating  = false; // true once ESCALATE control word detected
        var failedOpen  = false; // true when no control word found; triggers stronger reminder next turn

        await foreach (var delta in _chat.ChatStreamingAsync(messages, cancellationToken))
        {
            if (resolved)
            {
                // After ESCALATE, the LLM body is internal reasoning — not shown to the user.
                if (!escalating)
                {
                    answer.Append(delta);
                    yield return BotEvent.TextChunk(delta);
                }
                continue;
            }

            buffer.Append(delta);
            var text   = buffer.ToString();
            var newline = text.IndexOf('\n');

            if (newline >= 0)
            {
                var prefix = text[..newline].Trim();

                if (ResponseControl.StartsWith(prefix, ResponseControl.Escalate))
                {
                    _logger.LogDebug("BotWire: ESCALATE control word detected.");
                    escalating = resolved = true;
                    yield return BotEvent.Escalated();
                    continue; // LLM body after ESCALATE is internal reasoning, not shown to user
                }

                resolved = true;
                string emit;
                if (ResponseControl.StartsWith(prefix, ResponseControl.Answer))
                {
                    _logger.LogDebug("BotWire: ANSWER control word detected.");
                    emit = ResponseControl.Body(text, newline, ResponseControl.Answer);
                }
                else
                {
                    _logger.LogWarning(
                        "BotWire: stream prefix '{Prefix}' is not a recognized control word; failing open as ANSWER.",
                        prefix.Length > 60 ? prefix[..60] : prefix);
                    failedOpen = true;
                    emit = text;
                }

                EmitDelta(emit, answer);
                if (emit.Length > 0)
                    yield return BotEvent.TextChunk(emit);

                continue;
            }

            if (buffer.Length >= ResponseControl.ScanLimit)
            {
                _logger.LogWarning(
                    "BotWire: no control word in first {Limit} chars; buffer: '{Buffer}'; failing open as ANSWER.",
                    ResponseControl.ScanLimit,
                    text.Length > 80 ? text[..80] : text);
                resolved   = true;
                failedOpen = true;
                EmitDelta(text, answer);
                yield return BotEvent.TextChunk(text);
            }
        }

        // After stream: if ESCALATE was detected, either create ticket (contact known) or collect it.
        if (escalating)
        {
            if (contact is not null)
            {
                _logger.LogDebug("BotWire: ESCALATE — contact already known, creating ticket immediately.");
                var ticket = await _ticketGenerator.GenerateAsync(
                    session, message, contact, cancellationToken);
                await NotifyAsync(ticket, cancellationToken);
                var confirmMsg = _options.TicketConfirmedMessage.Replace("{ticketId}", ticket.TicketId);
                yield return BotEvent.TicketConfirmed(ticket.TicketId, confirmMsg);
                yield return BotEvent.Done(new AnswerResult(AnswerStatus.TicketCreated, ticket.TicketId));
            }
            else
            {
                _logger.LogDebug("BotWire: ESCALATE — contact unknown, emitting CollectContact.");
                yield return BotEvent.CollectContact();
            }
            yield break;
        }

        // Stream ended before the control word was resolved: a short reply with no trailing newline.
        if (!resolved)
        {
            var text    = buffer.ToString();
            var trimmed = text.TrimStart();

            if (ResponseControl.StartsWith(trimmed, ResponseControl.Escalate))
            {
                _logger.LogDebug("BotWire: ESCALATE control word detected (no trailing newline).");
                yield return BotEvent.Escalated();
                // LLM body after ESCALATE is internal reasoning, not shown to user.

                if (contact is not null)
                {
                    _logger.LogDebug("BotWire: ESCALATE (no newline) — contact known, creating ticket immediately.");
                    var ticket = await _ticketGenerator.GenerateAsync(
                        session, message, contact, cancellationToken);
                    await NotifyAsync(ticket, cancellationToken);
                    var confirmMsg = _options.TicketConfirmedMessage.Replace("{ticketId}", ticket.TicketId);
                    yield return BotEvent.TicketConfirmed(ticket.TicketId, confirmMsg);
                    yield return BotEvent.Done(new AnswerResult(AnswerStatus.TicketCreated, ticket.TicketId));
                }
                else
                {
                    yield return BotEvent.CollectContact();
                }
                yield break;
            }

            string emit;
            if (ResponseControl.StartsWith(trimmed, ResponseControl.Answer))
            {
                _logger.LogDebug("BotWire: ANSWER control word detected (no trailing newline).");
                emit = ResponseControl.Body(text, -1, ResponseControl.Answer);
            }
            else
            {
                if (text.Length > 0)
                    _logger.LogWarning(
                        "BotWire: stream ended with no recognized control word; failing open as ANSWER. Response: '{Response}'",
                        text.Length > 200 ? text[..200] : text);
                failedOpen = true;
                emit = text;
            }

            EmitDelta(emit, answer);
            if (emit.Length > 0)
                yield return BotEvent.TextChunk(emit);
        }

        // When triage just adjudicated the fail-open streak and chose to continue, reset the counter
        // (FailedOpen = false) so triage does not re-run on every subsequent turn.
        yield return BotEvent.Done(
            new AnswerResult(AnswerStatus.Answered, answer.ToString(), FailedOpen: failedOpen && !triageContinued));
    }

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

    private async Task<bool> TriageEscalationAsync(
        ConversationSession session,
        string currentMessage,
        CancellationToken ct)
    {
        var messages = new List<ChatMessage>(session.SendHistory.Count + 2);
        // Quote user messages so the triage LLM also treats them as data.
        foreach (var m in session.SendHistory)
            messages.Add(m.Role == ChatRole.User ? new(ChatRole.User, QuoteUserMessage(m.Content)) : m);
        messages.Add(new(ChatRole.User, QuoteUserMessage(currentMessage)));
        messages.Add(new(ChatRole.System,
            "Based only on the conversation above: does the customer have a problem that requires " +
            "a human agent (needs account access, order data, or has explicitly asked to speak to a person)? " +
            "Reply with exactly one word: YES or NO."));
        var raw = await _chat.ChatAsync(messages, ct);
        return raw.Trim().StartsWith("YES", StringComparison.OrdinalIgnoreCase);
    }

    private static string QuoteUserMessage(string content) =>
        $"{{\"user_message\": {JsonSerializer.Serialize(content)}}}";

    private static void EmitDelta(string delta, StringBuilder answer)
    {
        if (delta.Length > 0)
            answer.Append(delta);
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
        // had no control word, since the model is clearly drifting.
        var reminder = session.ConsecutiveNoControlWordCount >= 3
            ? $"CRITICAL ERROR: Your last {session.ConsecutiveNoControlWordCount} replies were ALL missing the required control word. " +
              $"The application is broken. You MUST start your reply with {ResponseControl.Answer} or " +
              $"{ResponseControl.Escalate} alone on the very first line — nothing before it, absolutely no exceptions."
            : session.ConsecutiveNoControlWordCount > 0
            ? $"CRITICAL ERROR: Your previous reply was missing the required control word. " +
              $"The application broke. You MUST start your reply with {ResponseControl.Answer} or " +
              $"{ResponseControl.Escalate} alone on the very first line — nothing before it, no exceptions."
            : $"REMINDER: Your reply MUST start with {ResponseControl.Answer} or {ResponseControl.Escalate} alone on the first line. Nothing before it.";
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
