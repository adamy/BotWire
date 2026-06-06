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
using BotWire.Core.Rag;
using BotWire.Core.Ticket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>DI registration helpers for the RAG document loader and answer provider.</summary>
public static class AnswerProviderServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DocumentLoader"/> as <see cref="IDocumentLoader"/> and
    /// <see cref="AnswerProvider"/> as <see cref="IAnswerProvider"/>, both singletons.
    /// Requires an <see cref="ILlmChatClient"/> to be registered separately.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Delegate to populate <see cref="AnswerProviderOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddBotWireAnswerProvider(
        this IServiceCollection services,
        Action<AnswerProviderOptions> configure)
    {
        services.AddOptions<AnswerProviderOptions>().Configure(configure).ValidateDataAnnotations().ValidateOnStart();

        services.AddSingleton<IDocumentLoader, DocumentLoader>();
        services.AddSingleton<TicketGenerator>(sp => new TicketGenerator(
            sp.GetRequiredService<ILlmChatClient>(),
            sp.GetRequiredService<IOptions<AnswerProviderOptions>>(),
            sp.GetRequiredService<ILogger<TicketGenerator>>()));
        services.AddSingleton<IAnswerProvider>(sp => new AnswerProvider(
            sp.GetRequiredService<ILlmChatClient>(),
            sp.GetRequiredService<IDocumentLoader>(),
            sp.GetRequiredService<TicketGenerator>(),
            sp.GetRequiredService<IOptions<AnswerProviderOptions>>(),
            sp.GetRequiredService<ILogger<AnswerProvider>>()));

        return services;
    }
}
