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

using BotWire.Core.Models;

namespace BotWire.Core.Abstractions;

/// <summary>Persists and retrieves conversation sessions keyed by a session token.</summary>
public interface IConversationStore
{
    /// <summary>Retrieves the session for the given token, or <see langword="null"/> if it does not exist.</summary>
    /// <param name="token">Opaque session identifier.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    Task<ConversationSession?> GetAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Persists or updates the session for the given token.</summary>
    /// <param name="token">Opaque session identifier.</param>
    /// <param name="session">The session state to store.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    Task SaveAsync(string token, ConversationSession session, CancellationToken cancellationToken = default);

    /// <summary>Removes the session for the given token. No-op if the session does not exist.</summary>
    /// <param name="token">Opaque session identifier.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    Task DeleteAsync(string token, CancellationToken cancellationToken = default);
}
