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
using BotWire.Core.Audit;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>DI registration helpers for the audit log.</summary>
public static class AuditLoggerServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="JsonFileAuditLogger"/> as the <see cref="IAuditLogger"/>, writing
    /// NDJSON to <paramref name="path"/>. Replaces the default <see cref="NullAuditLogger"/>.
    /// Chain after <c>AddBotWire(...)</c>:
    /// <code>services.AddBotWire(...).AddJsonAuditLog("logs/audit.ndjson");</code>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="path">Destination NDJSON file path. Created (with parent directories) if missing.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddJsonAuditLog(this IServiceCollection services, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Audit log path must not be empty.", nameof(path));

        services.RemoveAll<IAuditLogger>();
        services.AddSingleton<IAuditLogger>(sp =>
            new JsonFileAuditLogger(path, sp.GetRequiredService<ILogger<JsonFileAuditLogger>>()));

        return services;
    }
}
