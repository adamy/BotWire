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
using BotWire.Core.Llm;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Dependency-injection registration helpers for <see cref="OpenAILlmClient"/>.</summary>
public static class OpenAILlmClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="OpenAILlmClient"/> as a singleton and binds it to
    /// <see cref="ILlmClient"/>, <see cref="ILlmChatClient"/> and <see cref="ILlmEmbedClient"/>.
    /// Options are validated with their DataAnnotations (e.g. the required API key) on first resolve.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Delegate to populate <see cref="OpenAILlmClientOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddOpenAILlmClient(
        this IServiceCollection services,
        Action<OpenAILlmClientOptions> configure)
    {
        services.AddOptions<OpenAILlmClientOptions>()
            .Configure(configure)
            .ValidateDataAnnotations();

        services.AddSingleton<OpenAILlmClient>();
        services.AddSingleton<ILlmClient>(sp => sp.GetRequiredService<OpenAILlmClient>());
        services.AddSingleton<ILlmChatClient>(sp => sp.GetRequiredService<OpenAILlmClient>());
        services.AddSingleton<ILlmEmbedClient>(sp => sp.GetRequiredService<OpenAILlmClient>());

        return services;
    }
}
