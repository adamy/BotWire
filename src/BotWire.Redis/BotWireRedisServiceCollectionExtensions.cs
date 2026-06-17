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
using BotWire.Core.Guard;
using BotWire.Redis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>DI registration helpers for the Redis-backed BotWire stores.</summary>
public static class BotWireRedisServiceCollectionExtensions
{
    /// <summary>
    /// Registers a shared <see cref="IConnectionMultiplexer"/> and replaces the default
    /// in-memory stores with Redis-backed implementations:
    /// <list type="bullet">
    ///   <item><description><see cref="IConversationStore"/> — sessions stored in Redis with sliding TTL.</description></item>
    ///   <item><description><see cref="IRateLimitStore"/> — distributed counters for per-minute, per-IP, and daily-budget dimensions.</description></item>
    /// </list>
    /// Call after <c>AddBotWire(...)</c>. MaxConcurrentSessions remains per-container (see remarks).
    /// </summary>
    /// <remarks>
    /// <b>MaxConcurrentSessions</b> is intentionally kept per-container. A true distributed
    /// semaphore is fragile when containers crash (leaked permits). The per-container semaphore
    /// still queues callers within a container; across containers the aggregate concurrency is
    /// <c>MaxConcurrentSessions × replicaCount</c>. Set the value accordingly, or disable it
    /// (set to 0) and rely on the other dimensions for cost control.
    /// </remarks>
    /// <param name="services">The service collection returned by <c>AddBotWire</c>.</param>
    /// <param name="connectionString">StackExchange.Redis connection string, e.g. <c>"localhost:6379"</c>.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddBotWireRedis(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(connectionString));

        // Replace in-memory conversation store with Redis-backed one.
        services.AddSingleton<RedisConversationStore>();
        services.Replace(ServiceDescriptor.Singleton<IConversationStore>(sp =>
            sp.GetRequiredService<RedisConversationStore>()));

        // Register distributed rate-limit store; picked up by the RateLimiter factory.
        services.AddSingleton<IRateLimitStore>(sp =>
            new RedisRateLimitStore(sp.GetRequiredService<IConnectionMultiplexer>()));

        return services;
    }
}
