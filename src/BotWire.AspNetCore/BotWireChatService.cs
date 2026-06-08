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
    private readonly IpRateLimiter _rateLimiter;
    private readonly IOptions<BotWireOptions> _options;
    private readonly IOptions<PiiGuardOptions> _piiOptions;

    public BotWireChatService(
        IAnswerProvider answers,
        IConversationStore sessions,
        ISessionTokenService tokens,
        IPiiGuard piiGuard,
        IpRateLimiter rateLimiter,
        IOptions<BotWireOptions> options,
        IOptions<PiiGuardOptions> piiOptions)
    {
        _answers   = answers;
        _sessions  = sessions;
        _tokens    = tokens;
        _piiGuard  = piiGuard;
        _rateLimiter = rateLimiter;
        _options   = options;
        _piiOptions = piiOptions;
    }

    /// <summary>
    /// Runs the full non-streaming pipeline: guards → session resolve → answer → session save.
    /// Returns a <see cref="ChatResult"/> with <see cref="ChatResult.HttpStatusCode"/> != 200
    /// when a guard check fails; the caller maps that to an HTTP error response.
    /// </summary>
    public async Task<ChatResult> AnswerAsync(ChatRequest req, string clientIp, CancellationToken ct = default)
    {
        var guard = CheckGuards(req.Message, req.SessionToken, clientIp);
        if (guard is not null) return guard;

        var (token, session) = await ResolveSessionAsync(req.SessionToken, ct);
        if (session is null)
            return new ChatResult("Blocked", "Invalid session token.", "", null, 400);

        var contact = BuildContact(req.ContactEmail, session);

        var result = await _answers.AnswerAsync(req.Message, session, contact, ct);
        await SaveAnswerAsync(token, session, req.Message, result, ct);

        return result.Status switch
        {
            AnswerStatus.TicketCreated => new ChatResult("TicketCreated", null,          token, result.Message),
            AnswerStatus.NeedHuman     => new ChatResult("NeedHuman",     result.Message, token, null),
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
        var guard = CheckGuards(req.Message, req.SessionToken, clientIp);
        if (guard is not null)
            return new StreamPrep(guard, null, null, null, null);

        var (token, session) = await ResolveSessionAsync(req.SessionToken, ct);
        if (session is null)
            return new StreamPrep(
                new ChatResult("Blocked", "Invalid session token.", "", null, 400),
                null, null, null, null);

        var contact = BuildContact(req.ContactEmail, session);

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

    public Task CommitStreamAsync(
        StreamPrep prep,
        string accumulatedText,
        bool escalationStarted,
        string? confirmedTicketId,
        CancellationToken ct = default)
    {
        var session = prep.Session!;

        var updatedHistory = new List<ChatMessage>(session.History);
        // Skip empty user messages (e.g. the placeholder sent when submitting the contact form)
        if (!string.IsNullOrWhiteSpace(prep.UserMessage))
            updatedHistory.Add(new ChatMessage(ChatRole.User, prep.UserMessage!));
        if (accumulatedText.Length > 0)
            updatedHistory.Add(new ChatMessage(ChatRole.Assistant, accumulatedText));

        ConversationSession updatedSession;
        if (confirmedTicketId is not null)
        {
            updatedSession = session with
            {
                History = updatedHistory,
                EscalationPending = false,
                EscalationTriggerMessage = null,
            };
        }
        else if (escalationStarted)
        {
            updatedSession = session with
            {
                History = updatedHistory,
                EscalationPending = true,
                EscalationTriggerMessage = session.EscalationTriggerMessage ?? prep.UserMessage,
            };
        }
        else
        {
            updatedSession = session with { History = updatedHistory };
        }

        return _sessions.SaveAsync(prep.Token!, updatedSession, ct);
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
            return (new ChatResult("Blocked", "Too many requests.", "", null, 429), "", false);

        var name     = string.IsNullOrWhiteSpace(req.Name)     ? null : req.Name.Trim();
        var username = string.IsNullOrWhiteSpace(req.Username) ? null : req.Username.Trim();
        var email    = string.IsNullOrWhiteSpace(req.Email)    ? null : req.Email.Trim();

        ContactInfo? knownUser = null;
        if (name is not null || username is not null || email is not null)
            knownUser = new ContactInfo(email, null, name, username);

        var token   = _tokens.GenerateToken();
        var session = new ConversationSession([], DateTimeOffset.UtcNow, KnownUser: knownUser);
        await _sessions.SaveAsync(token, session, ct);

        return (null, token, NeedsName: name is null);
    }

    // ── Private helpers ─────────────────────────────────────────────────────────

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

    private ChatResult? CheckGuards(string message, string? sessionToken, string clientIp)
    {
        var opts = _options.Value;

        if (message.Length > opts.MaxMessageLength)
            return new ChatResult("Blocked", "Message too long.", sessionToken ?? "", null, 400);

        if (!_rateLimiter.IsAllowed(clientIp))
            return new ChatResult("Blocked", "Too many requests.", sessionToken ?? "", null, 429);

        var pii = _piiGuard.Check(message);
        if (pii.Blocked)
            return new ChatResult("Blocked", _piiOptions.Value.RejectionMessage, sessionToken ?? "", null, 400);

        return null;
    }

    private async Task<(string token, ConversationSession? session)> ResolveSessionAsync(
        string? sessionToken, CancellationToken ct)
    {
        if (sessionToken is null)
        {
            var token   = _tokens.GenerateToken();
            var session = new ConversationSession([], DateTimeOffset.UtcNow);
            await _sessions.SaveAsync(token, session, ct);
            return (token, session);
        }

        var existing = await _sessions.GetAsync(sessionToken, ct);
        return (sessionToken, existing); // existing == null → invalid token
    }

    private async Task SaveAnswerAsync(
        string token, ConversationSession session, string message, AnswerResult result, CancellationToken ct)
    {
        var updatedHistory = new List<ChatMessage>(session.History)
        {
            new(ChatRole.User, message),
        };
        if (result.Status != AnswerStatus.TicketCreated)
            updatedHistory.Add(new ChatMessage(ChatRole.Assistant, result.Message));

        var updatedSession = result.Status switch
        {
            AnswerStatus.NeedHuman => session with
            {
                History = updatedHistory,
                EscalationPending = true,
                EscalationTriggerMessage = session.EscalationTriggerMessage ?? message,
            },
            AnswerStatus.TicketCreated => session with
            {
                History = updatedHistory,
                EscalationPending = false,
                EscalationTriggerMessage = null,
            },
            _ => session with { History = updatedHistory },
        };

        await _sessions.SaveAsync(token, updatedSession, ct);
    }
}
