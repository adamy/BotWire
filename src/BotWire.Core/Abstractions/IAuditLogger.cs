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

namespace BotWire.Core.Abstractions;

/// <summary>
/// Write-only sink for business/compliance events (conversations, guard blocks, escalations,
/// rate-limit hits, errors). Distinct from application logging (<c>ILogger&lt;T&gt;</c>): audit
/// events are for the business owner, not the developer. BotWire never reads back what it writes —
/// querying is the host's responsibility.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Records a single audit event. Implementations must not throw — a failed audit write must
    /// never break the request it describes; log and swallow instead.
    /// </summary>
    Task LogAsync(AuditEvent evt, CancellationToken cancellationToken = default);
}

/// <summary>A single business event for the audit trail.</summary>
/// <param name="Timestamp">When the event occurred (UTC).</param>
/// <param name="EventType">One of the <see cref="AuditEventType"/> constants.</param>
/// <param name="SessionId">The session token the event belongs to, or empty when not session-scoped.</param>
/// <param name="Data">Event-specific fields, flattened into the output record alongside the envelope.</param>
public sealed record AuditEvent(
    DateTimeOffset Timestamp,
    string EventType,
    string SessionId,
    IReadOnlyDictionary<string, object?> Data);

/// <summary>Canonical <see cref="AuditEvent.EventType"/> values.</summary>
public static class AuditEventType
{
    /// <summary>A user message or an assistant reply.</summary>
    public const string Message = "message";

    /// <summary>A guard (PII, prompt injection, or topic classifier) blocked a message.</summary>
    public const string GuardBlocked = "guard_blocked";

    /// <summary>A support ticket was triggered (human needed or all providers failed).</summary>
    public const string Escalated = "escalated";

    /// <summary>A rate-limit dimension rejected or capped a request.</summary>
    public const string RateLimited = "rate_limited";

    /// <summary>A chat provider failed and the request fell over to the next provider.</summary>
    public const string ProviderFailover = "provider_failover";

    /// <summary>An unexpected error occurred while handling a request.</summary>
    public const string Error = "error";
}
