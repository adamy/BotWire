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
using BotWire.Channels.Email;
using BotWire.Core.Guard;
using BotWire.Core.Models;

namespace BotWire.AspNetCore;

/// <summary>Top-level configuration for the BotWire support bot.</summary>
public sealed class BotWireOptions
{
    /// <summary>Short description of the support topic, injected into the system prompt.</summary>
    [Required]
    public string TopicDescription { get; set; } = "";

    /// <summary>
    /// Language the AI writes escalation ticket summary/details in (the email a human agent reads),
    /// regardless of the customer's language. Free text passed to the model, e.g. <c>"English"</c>,
    /// <c>"简体中文"</c>. Defaults to <c>"English"</c>. Customer-facing chat replies always match the
    /// customer's own language and are not affected by this setting.
    /// </summary>
    public string TicketLanguage { get; set; } = "English";

    /// <summary>Paths to Markdown knowledge-base documents. At least one is required.</summary>
    public IList<string> Documents { get; set; } = [];

    /// <summary>Maximum character length of a single user message. Requests exceeding this are rejected.</summary>
    public int MaxMessageLength { get; set; } = 2000;

    /// <summary>Rate-limit cap per IP per minute.</summary>
    public int MaxRequestsPerIpPerMinute { get; set; } = 20;

    /// <summary>
    /// Five-dimension rate limiting (design 008): concurrent sessions, per-minute delay,
    /// per-session message cap, new-sessions-per-IP-per-hour, and a daily token budget.
    /// Each dimension is independently configurable and disabled when set to <c>0</c>.
    /// </summary>
    public RateLimitOptions RateLimiting { get; set; } = new();

    /// <summary>Idle TTL for conversation sessions.</summary>
    public TimeSpan SessionTtl { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// Number of recent messages kept verbatim in the history sent to the answer LLM.
    /// Once the send-history grows past twice this value, the oldest messages are folded into a
    /// single LLM-generated summary system message to bound token cost on long conversations.
    /// The full conversation is always preserved separately for ticket generation.
    /// Defaults to 20. Set to 0 to disable summary compression (not recommended).
    /// </summary>
    public int SummaryInterval { get; set; } = 20;

    /// <summary>Chat completion provider. Required at startup.</summary>
    public OpenAIProviderOptions? ChatProvider { get; set; }

    /// <summary>Embedding provider. Optional — skip to disable embedding-based retrieval.</summary>
    public OpenAIProviderOptions? EmbeddingProvider { get; set; }

    /// <summary>Email notification settings. When <see langword="null"/>, ticket escalation is disabled.</summary>
    public EmailOptions? Email { get; set; }

    /// <summary>CORS settings for BotWire API endpoints.</summary>
    public BotWireCorsOptions Cors { get; set; } = new();

    /// <summary>Prompt injection detection settings. Enabled by default.</summary>
    public PromptInjectionOptions PromptInjection { get; set; } = new();

    /// <summary>
    /// PII detection settings. Enabled by default — blocks user messages matching
    /// common personal-data patterns (email, phone, credit-card) before they reach
    /// the AI provider. Add your own patterns via <see cref="PiiGuardOptions.AdditionalPatterns"/>.
    /// </summary>
    public PiiGuardOptions PiiGuard { get; set; } = new();

    /// <summary>
    /// Message shown in the widget when a stream request fails.
    /// Defaults to <c>"Something went wrong. Please try again."</c>.
    /// </summary>
    public string ErrorMessage { get; set; } = "Something went wrong. Please try again.";

    /// <summary>
    /// Reply shown when a message is classified off-topic. Only takes effect when
    /// <see cref="TopicDescription"/> is set (which enables the topic guard).
    /// </summary>
    public string OffTopicResponse { get; set; } =
        "I'm sorry, I can only help with questions related to our support topics. " +
        "Is there something along those lines I can help you with?";

    /// <summary>
    /// Maximum number of LLM attempts for a single turn when the response is empty or invalid
    /// (malformed JSON or a blank message). After this many failures the turn escalates to a human
    /// instead of showing a blank answer. Defaults to 3.
    /// </summary>
    public int MaxAnswerAttempts { get; set; } = 3;

    /// <summary>
    /// Message shown to the customer in the widget after a support ticket is confirmed.
    /// Use <c>{ticketId}</c> as a placeholder for the ticket identifier.
    /// Defaults to <c>"✓ Support ticket {ticketId} created — we'll be in touch soon."</c>.
    /// </summary>
    public string TicketConfirmedMessage { get; set; } =
        "✓ Support ticket {ticketId} created — we'll be in touch soon.";

    /// <summary>
    /// Optional async callback invoked after a support ticket is created.
    /// Use this to integrate ticket creation into the host application — e.g. write to a
    /// database, push to a queue, or update a CRM.
    /// Exceptions are caught and logged; a failing callback does not surface an error to the user.
    /// </summary>
    public Func<SupportTicket, Task>? OnTicketCreated { get; set; }
}
