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

namespace BotWire.AspNetCore;

/// <summary>Top-level configuration for the BotWire support bot.</summary>
public sealed class BotWireOptions
{
    /// <summary>Short description of the support topic, injected into the system prompt.</summary>
    [Required]
    public string TopicDescription { get; set; } = "";

    /// <summary>Reply sent when the user asks an off-topic question.</summary>
    public string OffTopicResponse { get; set; } = "I can only help with support topics.";

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

    /// <summary>Idle TTL for conversation sessions.</summary>
    public TimeSpan SessionTtl { get; set; } = TimeSpan.FromHours(2);

    /// <summary>Chat completion provider. Required at startup.</summary>
    public OpenAIProviderOptions? ChatProvider { get; set; }

    /// <summary>Embedding provider. Optional — skip to disable embedding-based retrieval.</summary>
    public OpenAIProviderOptions? EmbeddingProvider { get; set; }

    /// <summary>Email notification settings. When <see langword="null"/>, ticket escalation is disabled.</summary>
    public EmailOptions? Email { get; set; }

    /// <summary>Optional public API key required on chat requests.</summary>
    public string? PublicKey { get; set; }

    /// <summary>Optional admin API key for privileged operations.</summary>
    public string? AdminKey { get; set; }

    /// <summary>CORS settings for BotWire API endpoints.</summary>
    public BotWireCorsOptions Cors { get; set; } = new();
}
