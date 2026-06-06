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

/// <summary>Delivers escalated support tickets through a specific notification channel.</summary>
public interface INotificationChannel
{
    /// <summary>Identifies the channel type, e.g. <c>email</c>, <c>slack</c>.</summary>
    string ChannelType { get; }

    /// <summary>Sends the escalated support ticket through this channel.</summary>
    /// <param name="ticket">The ticket to deliver.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    Task SendTicketAsync(SupportTicket ticket, CancellationToken cancellationToken = default);
}
