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

using System.Diagnostics;
using BotWire.Core.Abstractions;
using BotWire.Core.Audit;
using BotWire.Core.Conversation;
using BotWire.Core.Enums;
using BotWire.Core.Guard;
using BotWire.Core.Models;
using Microsoft.Extensions.Options;

namespace BotWire.AspNetCore;

/// <summary>
/// HTTP-status-aware result of a non-streaming chat request.
/// <see cref="HttpStatusCode"/> is 200 on success; 400 or 429 when a guard check failed.
/// </summary>
internal sealed record ChatResult(
    string Status,
    string? Message,
    string SessionToken,
    string? TicketId,
    int HttpStatusCode = 200);

/// <summary>
/// Result of <see cref="BotWireChatService.PrepareStreamAsync"/>: either an
/// <see cref="Error"/> (guard failure) or a fully-resolved streaming context.
/// </summary>
internal sealed record StreamPrep(
    ChatResult? Error,
    string? Token,
    ConversationSession? Session,
    ContactInfo? Contact,
    string? UserMessage);

/// <summary>
/// Handles the non-HTTP business logic of chat requests: guard pipeline,
/// session resolution, and session history persistence.
/// Endpoints remain thin HTTP glue that delegate to this service.
/// </summary>
internal sealed class BotWireChatService
{
    private readonly IAnswerProvider _answers;
    private readonly IConversationStore _sessions;
    private readonly ISessionTokenService _tokens;
    private readonly IPiiGuard _piiGuard;
    private readonly IPromptInjectionGuard _injectionGuard;
    private readonly IpRateLimiter _rateLimiter;
    private readonly ISummaryCompressor _compressor;
    private readonly IAuditLogger _audit;
    private readonly IOptions<BotWireOptions> _options;
    private readonly IOptions<PiiGuardOptions> _piiOptions;

    public BotWireChatService(
        IAnswerProvider answers,
        IConversationStore sessions,
        ISessionTokenService tokens,
        IPiiGuard piiGuard,
        IPromptInjectionGuard injectionGuard,
        IpRateLimiter rateLimiter,
        ISummaryCompressor compressor,
        IAuditLogger audit,
        IOptions<BotWireOptions> options,
        IOptions<PiiGuardOptions> piiOptions)
    {
        _answers        = answers;
        _sessions       = sessions;
        _tokens         = tokens;
        _piiGuard       = piiGuard;
        _injectionGuard = injectionGuard;
        _rateLimiter    = rateLimiter;
        _compressor     = compressor;
        _audit          = audit;
        _options        = options;
        _piiOptions     = piiOptions;
    }

    /// <summary>
    /// Runs the full non-streaming pipeline: guards → session resolve → answer → session save.
    /// Returns a <see cref="ChatResult"/> with <see cref="ChatResult.HttpStatusCode"/> != 200
    /// when a guard check fails; the caller maps that to an HTTP error response.
    /// </summary>
    public async Task<ChatResult> AnswerAsync(ChatRequest req, string clientIp, CancellationToken ct = default)
    {
        var guard = await CheckGuardsAsync(req.Message, req.SessionToken, clientIp, ct);
        if (guard is not null) return guard;

        var (token, session) = await ResolveSessionAsync(req.SessionToken, ct);
        if (session is null)
            return new ChatResult("InvalidSession", "Invalid session token.", "", null, 400);

        await _audit.LogAsync(AuditEvents.UserMessage(token, req.Message), ct);

        var contact = BuildContact(req.ContactEmail, session);

        AnswerResult result;
        var sw = Stopwatch.StartNew();
        try
        {
            result = await _answers.AnswerAsync(req.Message, session, contact, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _audit.LogAsync(AuditEvents.Error(token, ex.Message), CancellationToken.None);
            throw;
        }
        sw.Stop();

        await SaveAnswerAsync(token, session, req.Message, result, ct);

        await _audit.LogAsync(AuditEvents.AssistantMessage(token, result.Message, result.RawResponse, sw.ElapsedMilliseconds), ct);
        if (result.Status == AnswerStatus.NeedHuman)
            await _audit.LogAsync(AuditEvents.Escalated(token, "NEED_HUMAN"), ct);
        else if (result.Status == AnswerStatus.TicketCreated)
            await _audit.LogAsync(AuditEvents.Escalated(token, "NEED_HUMAN", result.Message), ct);

        return result.Status switch
        {
            AnswerStatus.TicketCreated => new ChatResult("TicketCreated", null,          token, result.Message),
            AnswerStatus.NeedHuman     => new ChatResult("NeedHuman",     result.Message, token, null),
            AnswerStatus.OffTopic      => new ChatResult("OffTopic",      result.Message, token, null),
            _                          => new ChatResult("Answered",      result.Message, token, null),
        };
    }

    /// <summary>
    /// First half of the streaming pipeline: guards → session resolve.
    /// Returns a <see cref="StreamPrep"/> whose <see cref="StreamPrep.Error"/> is non-null
    /// when a guard check fails; otherwise all context fields are populated for the caller
    /// to open the SSE stream and call <see cref="CommitStreamAsync"/> on completion.
    /// </summary>
    public async Task<StreamPrep> PrepareStreamAsync(ChatRequest req, string clientIp, CancellationToken ct = default)
    {
        var guard = await CheckGuardsAsync(req.Message, req.SessionToken, clientIp, ct);
        if (guard is not null)
            return new StreamPrep(guard, null, null, null, null);

        var (token, session) = await ResolveSessionAsync(req.SessionToken, ct);
        if (session is null)
            return new StreamPrep(
                new ChatResult("InvalidSession", "Invalid session token.", "", null, 400),
                null, null, null, null);

        var contact = BuildContact(req.ContactEmail, session);

        // Skip the empty placeholder sent when submitting the contact form.
        if (!string.IsNullOrWhiteSpace(req.Message))
            await _audit.LogAsync(AuditEvents.UserMessage(token, req.Message), ct);

        return new StreamPrep(null, token, session, contact, req.Message);
    }

    /// <summary>
    /// Second half of the streaming pipeline: saves session history after the SSE stream
    /// completes normally. Must NOT be called when <see cref="OperationCanceledException"/>
    /// interrupted the stream — a partial turn must not be persisted.
    /// </summary>
    /// <summary>
    /// Streams bot events for an already-prepared request.
    /// Call after <see cref="PrepareStreamAsync"/>; call <see cref="CommitStreamAsync"/> on completion.
    /// </summary>
    public IAsyncEnumerable<BotEvent> StreamEventsAsync(StreamPrep prep, CancellationToken ct)
        => _answers.StreamAsync(prep.UserMessage!, prep.Session!, prep.Contact, ct);

    public async Task CommitStreamAsync(
        StreamPrep prep,
        string accumulatedText,
        bool escalationStarted,
        string? confirmedTicketId,
        bool failedOpen = false,
        string? rawResponse = null,
        CancellationToken ct = default)
    {
        var session = prep.Session!;

        var newTurnFull = new List<ChatMessage>(2);
        var newTurnSend = new List<ChatMessage>(2);
        // Skip empty user messages (e.g. the placeholder sent when submitting the contact form)
        if (!string.IsNullOrWhiteSpace(prep.UserMessage))
        {
            var userTurn = new ChatMessage(ChatRole.User, prep.UserMessage!);
            newTurnFull.Add(userTurn);
            newTurnSend.Add(userTurn);
        }
        if (accumulatedText.Length > 0)
        {
            newTurnFull.Add(new ChatMessage(ChatRole.Assistant, accumulatedText));
            // Send-history carries the JSON envelope (falling back to the text if unavailable).
            newTurnSend.Add(new ChatMessage(ChatRole.Assistant, rawResponse ?? accumulatedText));
        }

        var (updatedFull, updatedSend) = await AppendAndCompressAsync(session, newTurnFull, newTurnSend, ct);

        ConversationSession updatedSession;
        if (confirmedTicketId is not null)
        {
            updatedSession = session with
            {
                FullHistory = updatedFull,
                SendHistory = updatedSend,
                EscalationPending = false,
                EscalationTriggerMessage = null,
                ConsecutiveNoControlWordCount = 0,
            };
        }
        else if (escalationStarted)
        {
            updatedSession = session with
            {
                FullHistory = updatedFull,
                SendHistory = updatedSend,
                EscalationPending = true,
                EscalationTriggerMessage = session.EscalationTriggerMessage ?? prep.UserMessage,
                ConsecutiveNoControlWordCount = 0,
            };
        }
        else
        {
            updatedSession = session with
            {
                FullHistory = updatedFull,
                SendHistory = updatedSend,
                ConsecutiveNoControlWordCount = failedOpen ? session.ConsecutiveNoControlWordCount + 1 : 0,
            };
        }

        await _sessions.SaveAsync(prep.Token!, updatedSession, ct);

        var sessionId = prep.Token!;
        if (accumulatedText.Length > 0)
            await _audit.LogAsync(AuditEvents.AssistantMessage(sessionId, accumulatedText, rawResponse), ct);
        if (confirmedTicketId is not null)
            await _audit.LogAsync(AuditEvents.Escalated(sessionId, "NEED_HUMAN", confirmedTicketId), ct);
        else if (escalationStarted)
            await _audit.LogAsync(AuditEvents.Escalated(sessionId, "NEED_HUMAN"), ct);
    }

    /// <summary>
    /// Creates a new session, optionally stamping it with identity supplied by the host.
    /// The host is responsible for populating <paramref name="req"/> from whatever auth
    /// system it uses — BotWire never reads ASP.NET Core claims or session directly.
    /// </summary>
    public async Task<(ChatResult? Error, string Token, bool NeedsName)> InitSessionAsync(
        InitSessionRequest req, string clientIp, CancellationToken ct = default)
    {
        if (!_rateLimiter.IsAllowed(clientIp))
        {
            await _audit.LogAsync(AuditEvents.RateLimited("", "MaxRequestsPerIpPerMinute"), ct);
            return (new ChatResult("Blocked", "Too many requests.", "", null, 429), "", false);
        }

        var name     = string.IsNullOrWhiteSpace(req.Name)     ? null : req.Name.Trim();
        var username = string.IsNullOrWhiteSpace(req.Username) ? null : req.Username.Trim();
        var email    = string.IsNullOrWhiteSpace(req.Email)    ? null : req.Email.Trim();

        ContactInfo? knownUser = null;
        if (name is not null || username is not null || email is not null)
            knownUser = new ContactInfo(email, null, name, username);

        var token   = _tokens.GenerateToken();
        var session = new ConversationSession([], [], DateTimeOffset.UtcNow, KnownUser: knownUser);
        await _sessions.SaveAsync(token, session, ct);

        return (null, token, NeedsName: name is null);
    }

    // ── Private helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Appends a completed turn to both histories, then runs summary compression on the
    /// send-history. <see cref="ConversationSession.FullHistory"/> is never compressed —
    /// ticket generation depends on the complete record.
    /// <para>
    /// The send-history assistant turn carries the raw JSON envelope the model emitted, not the
    /// plain display text. The model imitates its own prior turns, so a plain-text assistant
    /// history teaches it to drop the required JSON format on later turns (it starts replying with
    /// bare text or whitespace). Feeding the envelope back keeps every turn consistent with the
    /// mandated output format. The full-history turn keeps the readable text for ticket generation.
    /// </para>
    /// </summary>
    private async Task<(List<ChatMessage> Full, List<ChatMessage> Send)> AppendAndCompressAsync(
        ConversationSession session, List<ChatMessage> newTurnFull, List<ChatMessage> newTurnSend, CancellationToken ct)
    {
        var updatedFull = new List<ChatMessage>(session.FullHistory);
        updatedFull.AddRange(newTurnFull);

        var updatedSend = new List<ChatMessage>(session.SendHistory);
        updatedSend.AddRange(newTurnSend);
        updatedSend = await _compressor.CompressAsync(updatedSend, _options.Value.SummaryInterval, ct);

        return (updatedFull, updatedSend);
    }

    /// <summary>
    /// Builds the <see cref="ContactInfo"/> passed to <see cref="IAnswerProvider"/>.
    /// The caller's explicit email takes priority; name and username always come from
    /// the session's <see cref="ConversationSession.KnownUser"/> (set at session creation).
    /// Returns <see langword="null"/> when no email is resolvable — ticket creation requires
    /// at least an email address.
    /// <para>
    /// Design intent: when <see cref="ConversationSession.KnownUser"/> carries an email
    /// (injected by the host at session creation), the return value is non-null even without
    /// an explicit email on the request. This means a logged-in user's second turn after
    /// escalation (<c>EscalationPending == true</c>) auto-creates the ticket without prompting
    /// them to re-enter their address.
    /// </para>
    /// </summary>
    private static ContactInfo? BuildContact(string? explicitEmail, ConversationSession session)
    {
        var email = explicitEmail ?? session.KnownUser?.Email;
        if (email is null) return null;

        return new ContactInfo(
            email,
            null,
            session.KnownUser?.Name,
            session.KnownUser?.Username);
    }

    private async Task<ChatResult?> CheckGuardsAsync(
        string message, string? sessionToken, string clientIp, CancellationToken ct)
    {
        var opts = _options.Value;
        var sessionId = sessionToken ?? "";

        if (message.Length > opts.MaxMessageLength)
        {
            await _audit.LogAsync(AuditEvents.GuardBlocked(sessionId, "MaxMessageLength"), ct);
            return new ChatResult("Blocked", "Message too long.", sessionId, null, 400);
        }

        if (!_rateLimiter.IsAllowed(clientIp))
        {
            await _audit.LogAsync(AuditEvents.RateLimited(sessionId, "MaxRequestsPerIpPerMinute"), ct);
            return new ChatResult("Blocked", "Too many requests.", sessionId, null, 429);
        }

        var pii = _piiGuard.Check(message);
        if (pii.Blocked)
        {
            await _audit.LogAsync(AuditEvents.GuardBlocked(sessionId, "pii"), ct);
            return new ChatResult("Blocked", _piiOptions.Value.RejectionMessage, sessionId, null, 400);
        }

        if (_injectionGuard.IsInjectionAttempt(message))
        {
            await _audit.LogAsync(AuditEvents.GuardBlocked(sessionId, "prompt_injection"), ct);
            return new ChatResult("Blocked", _piiOptions.Value.RejectionMessage, sessionId, null, 400);
        }

        return null;
    }

    private async Task<(string token, ConversationSession? session)> ResolveSessionAsync(
        string? sessionToken, CancellationToken ct)
    {
        if (sessionToken is null)
        {
            var token   = _tokens.GenerateToken();
            var session = new ConversationSession([], [], DateTimeOffset.UtcNow);
            await _sessions.SaveAsync(token, session, ct);
            return (token, session);
        }

        var existing = await _sessions.GetAsync(sessionToken, ct);
        return (sessionToken, existing); // existing == null → invalid token
    }

    private async Task SaveAnswerAsync(
        string token, ConversationSession session, string message, AnswerResult result, CancellationToken ct)
    {
        var userTurn = new ChatMessage(ChatRole.User, message);
        var newTurnFull = new List<ChatMessage>(2) { userTurn };
        var newTurnSend = new List<ChatMessage>(2) { userTurn };
        if (result.Status != AnswerStatus.TicketCreated)
        {
            newTurnFull.Add(new ChatMessage(ChatRole.Assistant, result.Message));
            // Send-history carries the JSON envelope (falling back to the text if unavailable).
            newTurnSend.Add(new ChatMessage(ChatRole.Assistant, result.RawResponse ?? result.Message));
        }

        var (updatedFull, updatedSend) = await AppendAndCompressAsync(session, newTurnFull, newTurnSend, ct);

        var updatedSession = result.Status switch
        {
            AnswerStatus.NeedHuman => session with
            {
                FullHistory = updatedFull,
                SendHistory = updatedSend,
                EscalationPending = true,
                EscalationTriggerMessage = session.EscalationTriggerMessage ?? message,
                ConsecutiveNoControlWordCount = 0,
            },
            AnswerStatus.TicketCreated => session with
            {
                FullHistory = updatedFull,
                SendHistory = updatedSend,
                EscalationPending = false,
                EscalationTriggerMessage = null,
                ConsecutiveNoControlWordCount = 0,
            },
            _ => session with
            {
                FullHistory = updatedFull,
                SendHistory = updatedSend,
                ConsecutiveNoControlWordCount = result.FailedOpen ? session.ConsecutiveNoControlWordCount + 1 : 0,
            },
        };

        await _sessions.SaveAsync(token, updatedSession, ct);
    }
}
