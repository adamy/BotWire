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

namespace BotWire.AspNetCore;

/// <summary>Request body for the BotWire chat endpoints.</summary>
public sealed class ChatRequest
{
    /// <summary>The user's message text.</summary>
    public string Message { get; set; } = "";

    /// <summary>Session token from a previous response. Null to start a new session.</summary>
    public string? SessionToken { get; set; }

    /// <summary>User contact email for ticket escalation. Null on normal chat turns.</summary>
    public string? ContactEmail { get; set; }
}
