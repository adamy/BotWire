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

namespace BotWire.Channels.Email;

/// <summary>Configuration for the SMTP email notification channel.</summary>
public sealed class EmailOptions
{
    /// <summary>SMTP server hostname or IP address.</summary>
    [Required]
    public string SmtpHost { get; set; } = "";

    /// <summary>SMTP server port. Defaults to 587 (STARTTLS).</summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    /// <summary>
    /// When <see langword="true"/>, MailKit uses <c>SecureSocketOptions.Auto</c> (STARTTLS on 587,
    /// SSL on 465). Set to <see langword="false"/> for plain-text SMTP (e.g. Mailpit on port 1025).
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>Sender email address.</summary>
    [Required]
    public string FromAddress { get; set; } = "";

    /// <summary>Recipient email address for escalated tickets.</summary>
    [Required]
    public string ToAddress { get; set; } = "";

    /// <summary>SMTP username. Leave <see langword="null"/> for unauthenticated relays (e.g. Mailpit).</summary>
    public string? Username { get; set; }

    /// <summary>SMTP password. Leave <see langword="null"/> for unauthenticated relays.</summary>
    public string? Password { get; set; }

    /// <summary>Display name shown in the From header. Defaults to <c>BotWire Support</c>.</summary>
    public string FromName { get; set; } = "BotWire Support";
}
