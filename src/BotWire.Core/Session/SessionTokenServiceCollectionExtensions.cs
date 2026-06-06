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
using BotWire.Core.Session;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>DI registration helper for <see cref="SessionTokenService"/>.</summary>
public static class SessionTokenServiceCollectionExtensions
{
    /// <summary>Registers <see cref="SessionTokenService"/> as <see cref="ISessionTokenService"/>, singleton.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddBotWireSessionTokenService(this IServiceCollection services)
    {
        services.AddSingleton<ISessionTokenService, SessionTokenService>();
        return services;
    }
}
