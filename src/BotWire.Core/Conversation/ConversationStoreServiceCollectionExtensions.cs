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
using BotWire.Core.Conversation;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>DI registration helpers for the in-memory conversation store.</summary>
public static class ConversationStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="InMemoryConversationStore"/> as a singleton bound to
    /// <see cref="IConversationStore"/>. The store owns a background timer that evicts idle
    /// sessions; the DI container disposes it (stopping the timer) on shutdown. Options are
    /// validated with their DataAnnotations on first resolve.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional delegate to customise <see cref="ConversationStoreOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddInMemoryConversationStore(
        this IServiceCollection services,
        Action<ConversationStoreOptions>? configure = null)
    {
        var optionsBuilder = services.AddOptions<ConversationStoreOptions>()
            .ValidateDataAnnotations();

        if (configure is not null)
            optionsBuilder.Configure(configure);

        services.AddSingleton<InMemoryConversationStore>();
        services.AddSingleton<IConversationStore>(sp => sp.GetRequiredService<InMemoryConversationStore>());

        return services;
    }
}
