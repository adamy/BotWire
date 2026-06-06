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
using BotWire.Core.Abstractions;
using BotWire.Core.Enums;
using BotWire.Core.Models;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BotWire.Channels.Email.Tests;

public class EmailNotificationChannelTests
{
    private static SupportTicket MakeTicket() => new(
        "TKT-20260606-0001", "user msg", "AI summary", "Details.",
        TicketPriority.Medium, new ContactInfo("user@example.com", null),
        [], DateTimeOffset.UtcNow);

    private static EmailNotificationChannel CreateChannel(FakeSmtpSender fake, EmailOptions? opts = null)
    {
        opts ??= new EmailOptions
        {
            SmtpHost = "localhost",
            Port = 1025,
            UseSsl = false,
            FromAddress = "bot@example.com",
            ToAddress = "support@example.com",
        };
        return new EmailNotificationChannel(Options.Create(opts), new DefaultEmailTemplateFormatter(),
            () => fake, NullLogger<EmailNotificationChannel>.Instance);
    }

    [Fact]
    public async Task SendTicketAsync_ConnectsToConfiguredHost()
    {
        var fake = new FakeSmtpSender();
        await CreateChannel(fake).SendTicketAsync(MakeTicket());
        Assert.Equal("localhost", fake.ConnectedHost);
        Assert.Equal(1025, fake.ConnectedPort);
        Assert.Equal(SecureSocketOptions.None, fake.ConnectedOptions);
    }

    [Fact]
    public async Task SendTicketAsync_UseSslFalse_UsesNoneSocketOptions()
    {
        var fake = new FakeSmtpSender();
        await CreateChannel(fake).SendTicketAsync(MakeTicket());
        Assert.Equal(SecureSocketOptions.None, fake.ConnectedOptions);
    }

    [Fact]
    public async Task SendTicketAsync_UseSslTrue_UsesAutoSocketOptions()
    {
        var fake = new FakeSmtpSender();
        var opts = new EmailOptions
        {
            SmtpHost = "smtp.example.com", Port = 587, UseSsl = true,
            FromAddress = "bot@example.com", ToAddress = "support@example.com",
        };
        await CreateChannel(fake, opts).SendTicketAsync(MakeTicket());
        Assert.Equal(SecureSocketOptions.Auto, fake.ConnectedOptions);
    }

    [Fact]
    public async Task SendTicketAsync_NoAuthWhenCredentialsNull()
    {
        var fake = new FakeSmtpSender();
        await CreateChannel(fake).SendTicketAsync(MakeTicket());
        Assert.False(fake.AuthenticateCalled);
    }

    [Fact]
    public async Task SendTicketAsync_AuthenticatesWhenCredentialsProvided()
    {
        var fake = new FakeSmtpSender();
        var opts = new EmailOptions
        {
            SmtpHost = "smtp.example.com", Port = 587, UseSsl = true,
            FromAddress = "bot@example.com", ToAddress = "support@example.com",
            Username = "user", Password = "pass",
        };
        await CreateChannel(fake, opts).SendTicketAsync(MakeTicket());
        Assert.True(fake.AuthenticateCalled);
    }

    [Fact]
    public async Task SendTicketAsync_SendsMessageWithCorrectToAddress()
    {
        var fake = new FakeSmtpSender();
        await CreateChannel(fake).SendTicketAsync(MakeTicket());
        Assert.NotNull(fake.SentMessage);
        Assert.Contains("support@example.com", fake.SentMessage!.To.ToString());
    }

    [Fact]
    public async Task SendTicketAsync_SubjectContainsTicketId()
    {
        var fake = new FakeSmtpSender();
        await CreateChannel(fake).SendTicketAsync(MakeTicket());
        Assert.Contains("TKT-20260606-0001", fake.SentMessage!.Subject);
    }

    [Fact]
    public void SendTicketAsync_ChannelTypeIsEmail()
    {
        var fake = new FakeSmtpSender();
        Assert.Equal("email", CreateChannel(fake).ChannelType);
    }

    [Fact]
    public async Task SendTicketAsync_DisconnectsAfterSend()
    {
        var fake = new FakeSmtpSender();
        await CreateChannel(fake).SendTicketAsync(MakeTicket());
        Assert.True(fake.DisconnectCalled);
    }

    [Fact]
    public async Task SendTicketAsync_CustomFormatter_UsedForSubject()
    {
        var fake = new FakeSmtpSender();
        var opts = Options.Create(new EmailOptions
        {
            SmtpHost = "localhost", Port = 1025, UseSsl = false,
            FromAddress = "bot@example.com", ToAddress = "support@example.com",
        });
        var customFormatter = new LambdaFormatter(
            subject: _ => "CUSTOM SUBJECT",
            plain: t => "plain",
            html: t => "<b>html</b>");
        var channel = new EmailNotificationChannel(opts, customFormatter, () => fake,
            NullLogger<EmailNotificationChannel>.Instance);
        await channel.SendTicketAsync(MakeTicket());
        Assert.Equal("CUSTOM SUBJECT", fake.SentMessage!.Subject);
    }

    // ── Test doubles ─────────────────────────────────────────────────────────

    internal sealed class FakeSmtpSender : ISmtpSender
    {
        public string? ConnectedHost { get; private set; }
        public int ConnectedPort { get; private set; }
        public SecureSocketOptions ConnectedOptions { get; private set; }
        public bool AuthenticateCalled { get; private set; }
        public MimeMessage? SentMessage { get; private set; }
        public bool DisconnectCalled { get; private set; }

        public Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken ct)
        {
            ConnectedHost = host; ConnectedPort = port; ConnectedOptions = options;
            return Task.CompletedTask;
        }
        public Task AuthenticateAsync(string userName, string password, CancellationToken ct)
        {
            AuthenticateCalled = true;
            return Task.CompletedTask;
        }
        public Task SendAsync(MimeMessage message, CancellationToken ct) { SentMessage = message; return Task.CompletedTask; }
        public Task DisconnectAsync(bool quit, CancellationToken ct) { DisconnectCalled = true; return Task.CompletedTask; }
        public void Dispose() { }
    }

    private sealed class LambdaFormatter(
        Func<SupportTicket, string> subject,
        Func<SupportTicket, string> plain,
        Func<SupportTicket, string> html) : IEmailTemplateFormatter
    {
        public string FormatSubject(SupportTicket ticket) => subject(ticket);
        public string FormatPlainBody(SupportTicket ticket) => plain(ticket);
        public string FormatHtmlBody(SupportTicket ticket) => html(ticket);
    }
}
