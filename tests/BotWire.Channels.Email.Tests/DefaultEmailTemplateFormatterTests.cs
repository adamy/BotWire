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

using BotWire.Channels.Email;
using BotWire.Core.Enums;
using BotWire.Core.Models;

namespace BotWire.Channels.Email.Tests;

public class DefaultEmailTemplateFormatterTests
{
    private static readonly DefaultEmailTemplateFormatter Formatter = new();

    private static SupportTicket MakeTicket(
        string ticketId = "TKT-20260606-0001",
        string aiSummary = "Short summary",
        string details = "Full details here.",
        TicketPriority priority = TicketPriority.Medium,
        ContactInfo? contact = null,
        IReadOnlyList<ChatMessage>? history = null)
        => new(ticketId, "original user message", aiSummary, details, priority, contact,
               history ?? [], DateTimeOffset.Parse("2026-06-06T12:00:00Z"));

    // ----- FormatSubject -----

    [Fact]
    public void FormatSubject_ShortSummary_IncludesFullSummary()
    {
        var ticket = MakeTicket(aiSummary: "Short");
        var subject = Formatter.FormatSubject(ticket);
        Assert.Equal("[BotWire #TKT-20260606-0001] Short", subject);
    }

    [Fact]
    public void FormatSubject_LongSummary_TruncatesAt50Chars()
    {
        var ticket = MakeTicket(aiSummary: new string('A', 60));
        var subject = Formatter.FormatSubject(ticket);
        Assert.Equal($"[BotWire #TKT-20260606-0001] {new string('A', 50)}", subject);
    }

    [Fact]
    public void FormatSubject_SummaryExactly50Chars_NotTruncated()
    {
        var ticket = MakeTicket(aiSummary: new string('B', 50));
        var subject = Formatter.FormatSubject(ticket);
        Assert.Contains(new string('B', 50), subject);
        Assert.DoesNotContain(new string('B', 51), subject);
    }

    // ----- FormatPlainBody -----

    [Fact]
    public void FormatPlainBody_IncludesTicketId()
    {
        var ticket = MakeTicket();
        Assert.Contains("TKT-20260606-0001", Formatter.FormatPlainBody(ticket));
    }

    [Fact]
    public void FormatPlainBody_IncludesPriority()
    {
        var ticket = MakeTicket(priority: TicketPriority.High);
        Assert.Contains("High", Formatter.FormatPlainBody(ticket));
    }

    [Fact]
    public void FormatPlainBody_ContactProvided_ShowsEmail()
    {
        var ticket = MakeTicket(contact: new ContactInfo("user@example.com", null));
        Assert.Contains("user@example.com", Formatter.FormatPlainBody(ticket));
    }

    [Fact]
    public void FormatPlainBody_NoContact_ShowsNotProvided()
    {
        var ticket = MakeTicket(contact: null);
        Assert.Contains("not provided", Formatter.FormatPlainBody(ticket));
    }

    [Fact]
    public void FormatPlainBody_IncludesHistory()
    {
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "I need help"),
            new(ChatRole.Assistant, "How can I assist?"),
        };
        var ticket = MakeTicket(history: history);
        var body = Formatter.FormatPlainBody(ticket);
        Assert.Contains("[User]: I need help", body);
        Assert.Contains("[Assistant]: How can I assist?", body);
    }

    [Fact]
    public void FormatPlainBody_IncludesCreatedAt()
    {
        var ticket = MakeTicket();
        Assert.Contains("2026-06-06", Formatter.FormatPlainBody(ticket));
    }

    // ----- FormatHtmlBody -----

    [Fact]
    public void FormatHtmlBody_ContainsPreTag()
    {
        var ticket = MakeTicket();
        Assert.Contains("<pre", Formatter.FormatHtmlBody(ticket));
    }

    [Fact]
    public void FormatHtmlBody_HtmlEncodesSpecialChars()
    {
        var ticket = MakeTicket(aiSummary: "<script>alert('xss')</script>");
        var html = Formatter.FormatHtmlBody(ticket);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
