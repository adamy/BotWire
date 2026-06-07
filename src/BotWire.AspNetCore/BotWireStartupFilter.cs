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

using BotWire.Core.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace BotWire.AspNetCore;

/// <summary>
/// Validates <see cref="BotWireOptions"/> during application startup and throws
/// <see cref="BotWireConfigurationException"/> on the first misconfiguration found.
/// Runs before the middleware pipeline is built so problems surface at deploy time,
/// not on the first request.
/// </summary>
internal sealed class BotWireStartupFilter : IStartupFilter
{
    private readonly IOptions<BotWireOptions> _options;

    public BotWireStartupFilter(IOptions<BotWireOptions> options) => _options = options;

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            Validate(_options.Value);
            next(app);
        };
    }

    private static void Validate(BotWireOptions opts)
    {
        if (opts.ChatProvider is null)
            throw new BotWireConfigurationException(
                "BotWireOptions.ChatProvider is required. Configure an OpenAI-compatible chat provider.");

        if (opts.SessionTtl <= TimeSpan.Zero)
            throw new BotWireConfigurationException(
                "BotWireOptions.SessionTtl must be greater than zero.");

        if (opts.Documents.Count == 0)
            throw new BotWireConfigurationException(
                "BotWireOptions.Documents must contain at least one knowledge-base document path.");

        foreach (var path in opts.Documents)
        {
            string content;
            try
            {
                content = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                throw new BotWireConfigurationException(
                    $"Cannot read knowledge-base document '{path}'.", ex);
            }

            var estimatedTokens = content.Length / 4;
            if (estimatedTokens > 8_000)
                throw new BotWireConfigurationException(
                    $"Document '{path}' exceeds the 8 000-token limit " +
                    $"(estimated {estimatedTokens} tokens). Split it into smaller files.");
        }

        if (opts.Email is not null)
        {
            var e = opts.Email;
            if (string.IsNullOrWhiteSpace(e.SmtpHost))
                throw new BotWireConfigurationException(
                    "BotWireOptions.Email.SmtpHost is required when Email is configured.");
            if (string.IsNullOrWhiteSpace(e.FromAddress))
                throw new BotWireConfigurationException(
                    "BotWireOptions.Email.FromAddress is required when Email is configured.");
            if (string.IsNullOrWhiteSpace(e.ToAddress))
                throw new BotWireConfigurationException(
                    "BotWireOptions.Email.ToAddress is required when Email is configured.");
        }
    }
}
