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
using BotWire.Core.Models;

namespace BotWire.Core.Abstractions;

/// <summary>Processes user messages and produces bot answers, optionally as a stream.</summary>
public interface IAnswerProvider
{
    /// <summary>Processes a user message and returns a complete answer.</summary>
    /// <param name="message">The user's message text.</param>
    /// <param name="session">The current conversation session.</param>
    /// <param name="contact">
    /// Optional contact details supplied by the user. Required when
    /// <see cref="ConversationSession.EscalationPending"/> is true; not passed to the LLM.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>The bot's answer and its resolution status.</returns>
    Task<AnswerResult> AnswerAsync(string message, ConversationSession session, ContactInfo? contact = null, CancellationToken cancellationToken = default);

    /// <summary>Processes a user message and streams the response as a sequence of <see cref="BotEvent"/> values.</summary>
    /// <param name="message">The user's message text.</param>
    /// <param name="session">The current conversation session.</param>
    /// <param name="contact">
    /// Optional contact details supplied by the user. Required when
    /// <see cref="ConversationSession.EscalationPending"/> is true; not passed to the LLM.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the stream.</param>
    /// <returns>
    /// An async sequence of events: zero or more <see cref="BotEventKind.TextChunk"/> events followed by
    /// exactly one of these terminal sequences:
    /// <list type="bullet">
    ///   <item><see cref="BotEventKind.Done"/> — bot answered normally.</item>
    ///   <item><see cref="BotEventKind.Escalated"/> followed immediately by <see cref="BotEventKind.CollectContact"/> — both events form a single logical terminal; callers must consume the full sequence and must not stop at <see cref="BotEventKind.Escalated"/> alone.</item>
    ///   <item><see cref="BotEventKind.TicketConfirmed"/> followed by <see cref="BotEventKind.Done"/> — ticket was created; callers must clear <see cref="ConversationSession.EscalationPending"/> on the stored session.</item>
    /// </list>
    /// </returns>
    IAsyncEnumerable<BotEvent> StreamAsync(string message, ConversationSession session, ContactInfo? contact = null, CancellationToken cancellationToken = default);
}
