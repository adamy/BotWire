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

namespace BotWire.Core.Enums;

/// <summary>Discriminates the payload of a <see cref="BotWire.Core.Models.BotEvent"/>.</summary>
public enum BotEventKind
{
    /// <summary>A partial text token streamed from the LLM.</summary>
    TextChunk,

    /// <summary>The response is complete and a final <see cref="BotWire.Core.Models.AnswerResult"/> is available.</summary>
    Done,

    /// <summary>The bot is escalating the conversation; a <see cref="BotWire.Core.Models.SupportTicket"/> has been created.</summary>
    Escalated,

    /// <summary>An error occurred during processing.</summary>
    Error,
}
