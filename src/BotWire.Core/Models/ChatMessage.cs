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

using BotWire.Core.Enums;

namespace BotWire.Core.Models;

/// <summary>A single message in a conversation.</summary>
/// <param name="Role">The author role of this message.</param>
/// <param name="Content">The message text.</param>
/// <param name="Timestamp">Optional UTC timestamp of when the message was created.</param>
public sealed record ChatMessage(ChatRole Role, string Content, DateTimeOffset? Timestamp = null);
