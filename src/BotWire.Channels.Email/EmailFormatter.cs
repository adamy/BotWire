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

using System.Net;
using System.Text;
using BotWire.Core.Abstractions;
using BotWire.Core.Models;

namespace BotWire.Channels.Email;

/// <summary>
/// Built-in email template formatter. Register a custom <see cref="IEmailTemplateFormatter"/>
/// via DI to replace this with your own layout.
/// </summary>
public sealed class DefaultEmailTemplateFormatter : IEmailTemplateFormatter
{
    /// <inheritdoc/>
    public string FormatSubject(SupportTicket ticket)
    {
        var summary = ticket.AiSummary.Length <= 50
            ? ticket.AiSummary
            : ticket.AiSummary[..SurrogateAwareCut(ticket.AiSummary, 50)];
        return $"[BotWire #{ticket.TicketId}] {summary}";
    }

    /// <inheritdoc/>
    public string FormatPlainBody(SupportTicket ticket) => BuildPlainBody(ticket);

    /// <inheritdoc/>
    public string FormatHtmlBody(SupportTicket ticket)
    {
        var encoded = WebUtility.HtmlEncode(BuildPlainBody(ticket));
        return $"<html><body><pre style=\"font-family:monospace;white-space:pre-wrap\">{encoded}</pre></body></html>";
    }

    private static string BuildPlainBody(SupportTicket ticket)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Support Ticket: {ticket.TicketId}");
        sb.AppendLine($"Priority: {ticket.SuggestedPriority}");
        sb.AppendLine($"Contact: {(string.IsNullOrEmpty(ticket.Contact?.Email) ? "not provided" : ticket.Contact.Email)}");
        sb.AppendLine();
        sb.AppendLine("Summary:");
        sb.AppendLine(ticket.AiSummary);
        sb.AppendLine();
        sb.AppendLine("Details:");
        sb.AppendLine(ticket.Details);
        sb.AppendLine();
        if (ticket.History.Count > 0)
        {
            sb.AppendLine("Conversation History:");
            foreach (var msg in ticket.History)
                sb.AppendLine($"[{msg.Role}]: {msg.Content}");
            sb.AppendLine();
        }
        sb.AppendLine($"Created: {ticket.CreatedAt:u}");
        return sb.ToString();
    }

    // Avoids splitting a surrogate pair when truncating at a char boundary.
    private static int SurrogateAwareCut(string s, int max) =>
        char.IsHighSurrogate(s[max - 1]) ? max - 1 : max;
}
