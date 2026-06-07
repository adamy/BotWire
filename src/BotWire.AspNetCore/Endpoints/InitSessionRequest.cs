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

/// <summary>
/// Request body for <c>POST /support/session</c>.
/// The host populates whichever fields it has from its own auth system.
/// All fields are optional — send an empty body to create an anonymous session.
/// </summary>
public sealed class InitSessionRequest
{
    /// <summary>User's display name (e.g. "Jane Smith"). Used on support tickets.</summary>
    public string? Name { get; set; }

    /// <summary>Username or user ID from the host application.</summary>
    public string? Username { get; set; }

    /// <summary>Email address. When present, logged-in users skip the email prompt during escalation.</summary>
    public string? Email { get; set; }
}
