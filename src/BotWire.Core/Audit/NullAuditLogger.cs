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

namespace BotWire.Core.Audit;

/// <summary>
/// Default <see cref="IAuditLogger"/>: discards every event. Registered when no audit sink is
/// configured, so audit call sites incur effectively zero cost.
/// </summary>
public sealed class NullAuditLogger : IAuditLogger
{
    /// <summary>A shared instance; the logger holds no state.</summary>
    public static readonly NullAuditLogger Instance = new();

    /// <inheritdoc/>
    public Task LogAsync(AuditEvent evt, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
