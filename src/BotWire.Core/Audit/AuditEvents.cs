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

namespace BotWire.Core.Audit;

/// <summary>
/// Factory helpers that build <see cref="AuditEvent"/> instances with the canonical field names
/// for each event type, so call sites and the NDJSON output stay consistent.
/// </summary>
public static class AuditEvents
{
    /// <summary>A user message reaching the bot (after guards passed).</summary>
    public static AuditEvent UserMessage(string sessionId, string content) =>
        new(DateTimeOffset.UtcNow, AuditEventType.Message, sessionId, new Dictionary<string, object?>
        {
            ["role"] = "user",
            ["content"] = content,
        });

    /// <summary>An assistant reply, with its text and optionally the time taken to produce it.</summary>
    public static AuditEvent AssistantMessage(
        string sessionId, string content, long? latencyMs = null, string? provider = null) =>
        new(DateTimeOffset.UtcNow, AuditEventType.Message, sessionId, new Dictionary<string, object?>
        {
            ["role"] = "assistant",
            ["content"] = content,
            ["provider"] = provider,
            ["latencyMs"] = latencyMs,
        });

    /// <summary>A guard rejected a message before it reached the model.</summary>
    public static AuditEvent GuardBlocked(string sessionId, string guard) =>
        new(DateTimeOffset.UtcNow, AuditEventType.GuardBlocked, sessionId, new Dictionary<string, object?>
        {
            ["guard"] = guard,
        });

    /// <summary>A support ticket was raised, or escalation began awaiting contact details.</summary>
    public static AuditEvent Escalated(string sessionId, string reason, string? ticketId = null) =>
        new(DateTimeOffset.UtcNow, AuditEventType.Escalated, sessionId, new Dictionary<string, object?>
        {
            ["reason"] = reason,
            ["ticketId"] = ticketId,
        });

    /// <summary>A rate-limit dimension capped or rejected the request.</summary>
    public static AuditEvent RateLimited(string sessionId, string limit) =>
        new(DateTimeOffset.UtcNow, AuditEventType.RateLimited, sessionId, new Dictionary<string, object?>
        {
            ["limit"] = limit,
        });

    /// <summary>A provider failed and the request fell over to the next one.</summary>
    public static AuditEvent ProviderFailover(string sessionId, string from, string to, string reason) =>
        new(DateTimeOffset.UtcNow, AuditEventType.ProviderFailover, sessionId, new Dictionary<string, object?>
        {
            ["from"] = from,
            ["to"] = to,
            ["reason"] = reason,
        });

    /// <summary>An unexpected error while handling a request.</summary>
    public static AuditEvent Error(string sessionId, string message) =>
        new(DateTimeOffset.UtcNow, AuditEventType.Error, sessionId, new Dictionary<string, object?>
        {
            ["message"] = message,
        });
}
