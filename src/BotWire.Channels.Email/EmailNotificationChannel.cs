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

using BotWire.Core.Abstractions;
using BotWire.Core.Models;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BotWire.Channels.Email;

/// <summary>Sends escalated support tickets via SMTP using MailKit.</summary>
internal sealed class EmailNotificationChannel : INotificationChannel
{
    private readonly EmailOptions _options;
    private readonly IEmailTemplateFormatter _formatter;
    private readonly Func<ISmtpSender> _senderFactory;
    private readonly ILogger<EmailNotificationChannel> _logger;

    public string ChannelType => "email";

    /// <summary>Production constructor — DI uses this.</summary>
    public EmailNotificationChannel(
        IOptions<EmailOptions> options,
        IEmailTemplateFormatter formatter,
        ILogger<EmailNotificationChannel> logger)
        : this(options, formatter, () => new SmtpClientAdapter(), logger) { }

    /// <summary>Test constructor — injectable SMTP sender factory.</summary>
    internal EmailNotificationChannel(
        IOptions<EmailOptions> options,
        IEmailTemplateFormatter formatter,
        Func<ISmtpSender> senderFactory,
        ILogger<EmailNotificationChannel> logger)
    {
        _options = options.Value;
        _formatter = formatter;
        _senderFactory = senderFactory;
        _logger = logger;
    }

    public async Task SendTicketAsync(SupportTicket ticket, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(_options.ToAddress));
        message.Subject = _formatter.FormatSubject(ticket);
        message.Body = new BodyBuilder
        {
            TextBody = _formatter.FormatPlainBody(ticket),
            HtmlBody = _formatter.FormatHtmlBody(ticket),
        }.ToMessageBody();

        var socketOptions = _options.UseSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None;

        using var sender = _senderFactory();
        await sender.ConnectAsync(_options.SmtpHost, _options.Port, socketOptions, cancellationToken);
        if (_options.Username is not null && _options.Password is not null)
            await sender.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        await sender.SendAsync(message, cancellationToken);
        await sender.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("BotWire: ticket {TicketId} dispatched via email to {ToAddress}",
            ticket.TicketId, _options.ToAddress);
    }
}
