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
using BotWire.Core.Guard;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>DI registration helpers for PII guard and IP rate limiter.</summary>
public static class GuardServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IPiiGuard"/> and <see cref="IpRateLimiter"/> as singletons.
    /// When <see cref="PiiGuardOptions.Enabled"/> is <see langword="false"/>, a no-op
    /// <see cref="NullPiiGuard"/> is registered for <see cref="IPiiGuard"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configurePii">Optional delegate to configure <see cref="PiiGuardOptions"/>.</param>
    /// <param name="configureRateLimit">Optional delegate to configure <see cref="RateLimiterOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddBotWireGuard(
        this IServiceCollection services,
        Action<PiiGuardOptions>? configurePii = null,
        Action<RateLimiterOptions>? configureRateLimit = null)
    {
        var piiBuilder = services.AddOptions<PiiGuardOptions>();
        if (configurePii is not null) piiBuilder.Configure(configurePii);
        piiBuilder.ValidateDataAnnotations().ValidateOnStart();

        services.AddSingleton<IPiiGuard>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<PiiGuardOptions>>();
            var logger = sp.GetRequiredService<ILogger<RegexPiiGuard>>();
            if (!opts.Value.Enabled)
            {
                logger.LogInformation("BotWire: PiiGuard disabled.");
                return NullPiiGuard.Instance;
            }
            return new RegexPiiGuard(opts, logger);
        });

        var rlBuilder = services.AddOptions<RateLimiterOptions>();
        if (configureRateLimit is not null) rlBuilder.Configure(configureRateLimit);
        rlBuilder.ValidateDataAnnotations().ValidateOnStart();

        services.AddSingleton<IpRateLimiter>();

        return services;
    }
}
