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

using System.Buffers.Text;
using System.Security.Cryptography;
using BotWire.Core.Abstractions;

namespace BotWire.Core.Session;

/// <summary>Generates cryptographically random, URL-safe session tokens.</summary>
public sealed class SessionTokenService : ISessionTokenService
{
    /// <summary>
    /// Returns a new cryptographically random token: 32 bytes of entropy encoded as Base64Url,
    /// producing a 43-character string with no padding and no characters that require URL encoding.
    /// </summary>
    public string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64Url.EncodeToString(bytes);
    }
}
