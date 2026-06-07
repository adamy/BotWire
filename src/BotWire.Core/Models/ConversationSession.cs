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

namespace BotWire.Core.Models;

/// <summary>Holds the in-progress conversation state for a single user session.</summary>
/// <param name="History">Ordered list of messages exchanged in this session.</param>
/// <param name="LastActivity">UTC timestamp of the most recent activity in this session.</param>
/// <param name="EscalationPending">
/// True when the bot has emitted an ESCALATE control word and is waiting for the user to
/// supply contact details before a support ticket can be generated.
/// </param>
/// <param name="EscalationTriggerMessage">
/// The user message that caused the escalation; used as <see cref="SupportTicket.UserMessage"/>
/// when the ticket is created. Null when <see cref="EscalationPending"/> is false.
/// </param>
/// <param name="KnownUser">
/// Identity injected by the host at session creation (e.g. from the host's own auth system).
/// Not added to chat history and never sent to the LLM.
/// When non-null and <see cref="ContactInfo.Email"/> is set, ticket escalation can proceed
/// without the user needing to re-enter their email.
/// </param>
public sealed record ConversationSession(
    List<ChatMessage> History,
    DateTimeOffset LastActivity,
    bool EscalationPending = false,
    string? EscalationTriggerMessage = null,
    ContactInfo? KnownUser = null);
