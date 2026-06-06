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

using MailKit.Security;
using MimeKit;

namespace BotWire.Channels.Email;

/// <summary>Narrow SMTP operations used by <see cref="EmailNotificationChannel"/>.</summary>
internal interface ISmtpSender : IDisposable
{
    Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken ct);
    Task AuthenticateAsync(string userName, string password, CancellationToken ct);
    Task SendAsync(MimeMessage message, CancellationToken ct);
    Task DisconnectAsync(bool quit, CancellationToken ct);
}
