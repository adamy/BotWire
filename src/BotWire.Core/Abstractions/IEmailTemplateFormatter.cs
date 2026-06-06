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

using BotWire.Core.Models;

namespace BotWire.Core.Abstractions;

/// <summary>
/// Formats a <see cref="SupportTicket"/> into an email subject and body.
/// Register a custom implementation via DI to override the default formatting.
/// </summary>
public interface IEmailTemplateFormatter
{
    /// <summary>Returns the email subject line for the given ticket.</summary>
    string FormatSubject(SupportTicket ticket);

    /// <summary>Returns the plain-text email body for the given ticket.</summary>
    string FormatPlainBody(SupportTicket ticket);

    /// <summary>Returns the HTML email body for the given ticket.</summary>
    string FormatHtmlBody(SupportTicket ticket);
}
