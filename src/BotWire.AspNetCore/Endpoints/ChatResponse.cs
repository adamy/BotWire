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

using System.Text.Json.Serialization;

namespace BotWire.AspNetCore;

/// <summary>Response body for the BotWire chat endpoints.</summary>
public sealed class ChatResponse
{
    /// <summary>Outcome: <c>Answered</c>, <c>NeedHuman</c>, <c>TicketCreated</c>, or <c>Blocked</c>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; }

    /// <summary>Bot response text. Null when <see cref="Status"/> is <c>TicketCreated</c>.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; }

    /// <summary>Session token. Include in subsequent requests to continue the conversation.</summary>
    [JsonPropertyName("sessionToken")]
    public string SessionToken { get; }

    /// <summary>Created ticket identifier. Non-null only when <see cref="Status"/> is <c>TicketCreated</c>.</summary>
    [JsonPropertyName("ticketId")]
    public string? TicketId { get; }

    public ChatResponse(string status, string? message, string sessionToken, string? ticketId = null)
    {
        Status = status;
        Message = message;
        SessionToken = sessionToken;
        TicketId = ticketId;
    }
}
