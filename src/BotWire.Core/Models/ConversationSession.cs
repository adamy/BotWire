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
public sealed record ConversationSession(
    List<ChatMessage> History,
    DateTimeOffset LastActivity);
