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

namespace BotWire.AspNetCore.Tests.Fakes;

/// <summary>Captures every audit event in order, for assertions.</summary>
internal sealed class FakeAuditLogger : IAuditLogger
{
    public List<AuditEvent> Events { get; } = [];

    public Task LogAsync(AuditEvent evt, CancellationToken cancellationToken = default)
    {
        Events.Add(evt);
        return Task.CompletedTask;
    }

    public IReadOnlyList<AuditEvent> OfType(string eventType) =>
        Events.Where(e => e.EventType == eventType).ToList();
}
