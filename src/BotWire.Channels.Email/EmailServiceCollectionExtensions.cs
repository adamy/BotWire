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

using BotWire.Channels.Email;
using BotWire.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>DI registration helpers for the email notification channel.</summary>
public static class EmailServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="INotificationChannel"/> (email) and <see cref="IEmailTemplateFormatter"/>
    /// (<see cref="DefaultEmailTemplateFormatter"/>) as singletons.
    /// Register a custom <see cref="IEmailTemplateFormatter"/> <b>before</b> this call to
    /// override the built-in email template.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Delegate to configure <see cref="EmailOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    /// <remarks>Each call registers a new <see cref="INotificationChannel"/> singleton.
    /// Calling this method more than once adds a second email channel, which is typically unintended.</remarks>
    public static IServiceCollection AddBotWireEmail(
        this IServiceCollection services,
        Action<EmailOptions> configure)
    {
        services.AddOptions<EmailOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // TryAdd so a custom formatter registered before this call is preserved.
        services.TryAddSingleton<IEmailTemplateFormatter, DefaultEmailTemplateFormatter>();

        services.AddSingleton<INotificationChannel>(sp =>
            new EmailNotificationChannel(
                sp.GetRequiredService<IOptions<EmailOptions>>(),
                sp.GetRequiredService<IEmailTemplateFormatter>(),
                sp.GetRequiredService<ILogger<EmailNotificationChannel>>()));

        return services;
    }
}
