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

using System.ComponentModel.DataAnnotations;
using BotWire.Core.Abstractions;
using BotWire.Core.Models;

namespace BotWire.Core.Rag;

/// <summary>Configuration for the RAG <see cref="AnswerProvider"/> (Phase 1, Mode A).</summary>
public sealed class AnswerProviderOptions
{
    /// <summary>
    /// Paths to the Markdown (<c>.md</c>) documents that form the bot's knowledge base. Their
    /// combined content is embedded verbatim in the system prompt (Mode A — no retrieval).
    /// </summary>
    public IList<string> DocumentPaths { get; set; } = [];

    /// <summary>
    /// Optional extra instructions prepended to the generated system prompt, e.g. a persona or
    /// tone guideline. The control-word protocol instruction is always appended by BotWire.
    /// </summary>
    public string? SystemPromptPreamble { get; set; }

    /// <summary>
    /// Prefix used when generating support ticket IDs, e.g. <c>"TKT"</c> produces
    /// <c>TKT-20260606-0001</c>. Must be at least one character. Defaults to <c>"TKT"</c>.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string TicketPrefix { get; set; } = "TKT";

    /// <summary>
    /// Language the AI writes the ticket summary and details in, regardless of the language the
    /// customer used (the human agent reading the ticket gets a consistent language). Free text
    /// passed to the model, e.g. <c>"English"</c>, <c>"简体中文"</c>, <c>"Français"</c>.
    /// Defaults to <c>"English"</c>.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string TicketLanguage { get; set; } = "English";

    /// <summary>
    /// Message shown to the customer in the widget after a support ticket is confirmed.
    /// Use <c>{ticketId}</c> as a placeholder for the ticket identifier.
    /// Defaults to <c>"✓ Support ticket {ticketId} created — we'll be in touch soon."</c>.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string TicketConfirmedMessage { get; set; } =
        "✓ Support ticket {ticketId} created — we'll be in touch soon.";

    /// <summary>
    /// Message used when the bot escalates to a human (the model returned <c>action: "escalate"</c>,
    /// or every answer attempt was empty/invalid). Set to an empty string to show no message before
    /// the contact form. Defaults to <c>"Let me connect you with our support team."</c>.
    /// </summary>
    public string AutoEscalationMessage { get; set; } =
        "Let me connect you with our support team.";

    /// <summary>
    /// Maximum number of LLM attempts for a single turn when the response is empty or invalid
    /// (malformed JSON, or a blank message). After this many empty/invalid responses the turn is
    /// escalated to a human instead of showing a blank answer. Defaults to <c>3</c>.
    /// </summary>
    public int MaxAnswerAttempts { get; set; } = 3;

    /// <summary>
    /// When true, the answer model is asked to classify each message as on- or off-topic and
    /// off-topic messages are answered with <see cref="OffTopicResponse"/> instead of the knowledge
    /// base. Enabled automatically when a topic description is configured.
    /// </summary>
    public bool TopicGuardEnabled { get; set; }

    /// <summary>
    /// Message shown to the customer when their message is classified off-topic (only used when
    /// <see cref="TopicGuardEnabled"/> is true).
    /// </summary>
    public string OffTopicResponse { get; set; } =
        "I'm sorry, I can only help with questions related to our support topics. " +
        "Is there something along those lines I can help you with?";

    /// <summary>
    /// Optional async callback invoked after a support ticket is created and all
    /// <see cref="INotificationChannel"/> dispatches have completed (or failed).
    /// Use this to integrate ticket creation into the host application — e.g. write to a
    /// database, push to a queue, or update a CRM — without implementing a full
    /// <see cref="INotificationChannel"/>.
    /// Exceptions thrown by this callback are caught, logged, and swallowed so that a
    /// failing host callback does not surface an error to the end user.
    /// </summary>
    public Func<SupportTicket, Task>? OnTicketCreated { get; set; }
}
