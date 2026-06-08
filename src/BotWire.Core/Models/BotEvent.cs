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

/// <summary>
/// A single event emitted by <see cref="Abstractions.IAnswerProvider.StreamAsync"/>.
/// Inspect <see cref="Kind"/> to determine which payload properties are populated.
/// </summary>
public sealed record BotEvent
{
    /// <summary>Discriminator that identifies the type of this event.</summary>
    public required BotEventKind Kind { get; init; }

    /// <summary>Streamed text token. Populated when <see cref="Kind"/> is <see cref="BotEventKind.TextChunk"/>.</summary>
    public string? Text { get; init; }

    /// <summary>Final answer result. Populated when <see cref="Kind"/> is <see cref="BotEventKind.Done"/>.</summary>
    public AnswerResult? Result { get; init; }

    /// <summary>Escalation ticket. May be populated when <see cref="Kind"/> is <see cref="BotEventKind.Escalated"/>.</summary>
    public SupportTicket? Ticket { get; init; }

    /// <summary>Reason the response was blocked. Populated when <see cref="Kind"/> is <see cref="BotEventKind.Blocked"/>.</summary>
    public string? Reason { get; init; }

    /// <summary>Confirmed support ticket identifier. Populated when <see cref="Kind"/> is <see cref="BotEventKind.TicketConfirmed"/>.</summary>
    public string? TicketId { get; init; }

    /// <summary>Customer-facing confirmation message. Populated when <see cref="Kind"/> is <see cref="BotEventKind.TicketConfirmed"/>.</summary>
    public string? ConfirmationMessage { get; init; }

    /// <summary>Error message. Populated when <see cref="Kind"/> is <see cref="BotEventKind.Error"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Creates a <see cref="BotEventKind.TextChunk"/> event.</summary>
    /// <param name="text">The partial text token.</param>
    public static BotEvent TextChunk(string text) =>
        new() { Kind = BotEventKind.TextChunk, Text = text };

    /// <summary>Creates a <see cref="BotEventKind.Done"/> event.</summary>
    /// <param name="result">The final answer result.</param>
    public static BotEvent Done(AnswerResult result) =>
        new() { Kind = BotEventKind.Done, Result = result };

    /// <summary>Creates a <see cref="BotEventKind.Escalated"/> event with an attached ticket.</summary>
    /// <param name="ticket">The support ticket generated for the escalation.</param>
    public static BotEvent Escalated(SupportTicket ticket) =>
        new() { Kind = BotEventKind.Escalated, Ticket = ticket };

    /// <summary>Creates a <see cref="BotEventKind.Escalated"/> event with no ticket yet (the escalation flow has only just begun).</summary>
    public static BotEvent Escalated() =>
        new() { Kind = BotEventKind.Escalated };

    /// <summary>Creates a <see cref="BotEventKind.CollectContact"/> event.</summary>
    public static BotEvent CollectContact() =>
        new() { Kind = BotEventKind.CollectContact };

    /// <summary>Creates a <see cref="BotEventKind.Blocked"/> event.</summary>
    /// <param name="reason">Why the response was blocked.</param>
    public static BotEvent Blocked(string reason) =>
        new() { Kind = BotEventKind.Blocked, Reason = reason };

    /// <summary>Creates a <see cref="BotEventKind.TicketConfirmed"/> event.</summary>
    /// <param name="ticketId">The identifier of the confirmed ticket.</param>
    /// <param name="confirmationMessage">Customer-facing message to display in the widget.</param>
    public static BotEvent TicketConfirmed(string ticketId, string confirmationMessage) =>
        new() { Kind = BotEventKind.TicketConfirmed, TicketId = ticketId, ConfirmationMessage = confirmationMessage };

    /// <summary>Creates a <see cref="BotEventKind.Error"/> event.</summary>
    /// <param name="message">A description of the error.</param>
    public static BotEvent Error(string message) =>
        new() { Kind = BotEventKind.Error, ErrorMessage = message };
}
