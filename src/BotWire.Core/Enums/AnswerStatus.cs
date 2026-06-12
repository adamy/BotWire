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

/// <summary>Indicates whether the bot answered successfully or needs to escalate to a human agent.</summary>
public enum AnswerStatus
{
    /// <summary>The bot provided a satisfactory answer.</summary>
    Answered,

    /// <summary>The bot could not answer and the conversation should be escalated to a human agent.</summary>
    NeedHuman,

    /// <summary>
    /// The message was classified off-topic by the topic guard. The configured off-topic response
    /// is shown and the message is not answered from the knowledge base.
    /// </summary>
    OffTopic,

    /// <summary>
    /// A support ticket was created after the user supplied contact details.
    /// Callers must clear <see cref="BotWire.Core.Models.ConversationSession.EscalationPending"/> on the
    /// stored session to prevent duplicate ticket generation on the next call.
    /// </summary>
    TicketCreated,
}
