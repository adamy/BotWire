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

/// <summary>Response body for <c>POST /support/session</c>.</summary>
public sealed class InitSessionResponse
{
    /// <summary>Session token. Pass in subsequent chat requests and store in <c>botwire_session</c> cookie.</summary>
    [JsonPropertyName("sessionToken")]
    public string SessionToken { get; }

    /// <summary>
    /// <see langword="true"/> when no display name was supplied.
    /// The widget should prompt the user for their name before the first chat message.
    /// </summary>
    [JsonPropertyName("needsName")]
    public bool NeedsName { get; }

    /// <summary>Localised error message to display in the widget when a stream request fails.</summary>
    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; }

    public InitSessionResponse(string sessionToken, bool needsName, string errorMessage)
    {
        SessionToken  = sessionToken;
        NeedsName     = needsName;
        ErrorMessage  = errorMessage;
    }
}
