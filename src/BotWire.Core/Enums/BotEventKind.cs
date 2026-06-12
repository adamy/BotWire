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

    /// <summary>The bot is escalating the conversation to a human. A <see cref="BotWire.Core.Models.SupportTicket"/> may be attached once one has been created.</summary>
    Escalated,

    /// <summary>The bot is requesting the user's contact details so a human can follow up.</summary>
    CollectContact,

    /// <summary>The response was blocked (e.g. by a safety or policy filter); <see cref="BotWire.Core.Models.BotEvent.Reason"/> explains why.</summary>
    Blocked,

    /// <summary>A support ticket was created and confirmed; <see cref="BotWire.Core.Models.BotEvent.TicketId"/> identifies it.</summary>
    TicketConfirmed,

    /// <summary>An error occurred during processing.</summary>
    Error,

    /// <summary>
    /// Reports the token usage for the turn. Carried by <see cref="BotWire.Core.Models.BotEvent.TokensUsed"/>.
    /// Internal accounting only (rate-limit budget + audit); not rendered to the client. Used by paths
    /// that have no <see cref="Done"/> event to surface usage on, e.g. escalation that ends in a
    /// contact-collection prompt.
    /// </summary>
    Usage,
}
