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

using System.Net.Http.Json;
using BotWire.Channels.Email;
using BotWire.Core.Enums;
using BotWire.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BotWire.Channels.Email.Tests;

/// <summary>
/// Integration tests against Mailpit. Start Mailpit before running:
///   docker run -p 1025:1025 -p 8025:8025 axllent/mailpit
/// Tests skip silently if Mailpit is not reachable.
/// </summary>
public class EmailNotificationChannelIntegrationTests
{
    private const string MailpitApiBase = "http://localhost:8025/api/v1";

    private static SupportTicket MakeTicket() => new(
        "INTEG-20260606-0001",
        "My order has not arrived",
        "Order not arrived after 2 weeks",
        "Customer placed order on 2026-05-23 and has not received it.",
        TicketPriority.High,
        new ContactInfo("customer@example.com", null),
        [
            new ChatMessage(ChatRole.User, "Where is my order?"),
            new ChatMessage(ChatRole.Assistant, "Let me escalate this for you."),
        ],
        DateTimeOffset.UtcNow);

    [Fact]
    public async Task SendTicketAsync_Mailpit_EmailIsReceived()
    {
        using var http = new HttpClient();

        // Skip if Mailpit is not running
        try { await http.GetAsync($"{MailpitApiBase}/messages"); }
        catch { return; }

        // Clear inbox before test
        await http.DeleteAsync($"{MailpitApiBase}/messages");

        var opts = Options.Create(new EmailOptions
        {
            SmtpHost = "localhost",
            Port = 1025,
            UseSsl = false,
            FromAddress = "bot@botwire.test",
            ToAddress = "support@botwire.test",
            FromName = "BotWire Test",
        });

        var channel = new EmailNotificationChannel(
            opts,
            new DefaultEmailTemplateFormatter(),
            NullLogger<EmailNotificationChannel>.Instance);

        await channel.SendTicketAsync(MakeTicket());

        // Allow Mailpit a moment to process
        await Task.Delay(200);

        var response = await http.GetFromJsonAsync<MailpitMessages>($"{MailpitApiBase}/messages");
        Assert.NotNull(response);
        Assert.True(response.Total >= 1, "Expected at least one message in Mailpit inbox.");

        var latest = response.Messages.First();
        Assert.Contains("INTEG-20260606-0001", latest.Subject);
        Assert.Equal("support@botwire.test", latest.To.First().Address);
    }

    private sealed record MailpitMessages(
        [property: System.Text.Json.Serialization.JsonPropertyName("messages")] IReadOnlyList<MailpitMessage> Messages,
        [property: System.Text.Json.Serialization.JsonPropertyName("total")] int Total);

    private sealed record MailpitMessage(
        [property: System.Text.Json.Serialization.JsonPropertyName("Subject")] string Subject,
        [property: System.Text.Json.Serialization.JsonPropertyName("To")] IReadOnlyList<MailpitAddress> To);

    private sealed record MailpitAddress(
        [property: System.Text.Json.Serialization.JsonPropertyName("Address")] string Address);
}
