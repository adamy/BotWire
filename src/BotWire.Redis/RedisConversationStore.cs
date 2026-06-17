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

using System.Text.Json;
using System.Text.Json.Serialization;
using BotWire.Core.Abstractions;
using BotWire.Core.Conversation;
using BotWire.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BotWire.Redis;

/// <summary>
/// Redis-backed <see cref="IConversationStore"/>. Sessions are JSON-serialised and stored with a
/// sliding TTL equal to <see cref="ConversationStoreOptions.SessionTtl"/>; the TTL is refreshed on
/// every <see cref="SaveAsync"/> call. A missing or expired key is returned as <see langword="null"/>.
/// Redis errors are surfaced as exceptions — callers should treat them as transient failures
/// (e.g. return 503) rather than silently losing conversation history.
/// </summary>
internal sealed class RedisConversationStore : IConversationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IConnectionMultiplexer _mux;
    private readonly TimeSpan _ttl;
    private readonly int _maxHistory;
    private readonly ILogger<RedisConversationStore> _logger;

    public RedisConversationStore(
        IConnectionMultiplexer mux,
        IOptions<ConversationStoreOptions> options,
        ILogger<RedisConversationStore> logger)
    {
        _mux = mux;
        _ttl = options.Value.SessionTtl;
        _maxHistory = options.Value.MaxHistoryMessages;
        _logger = logger;
    }

    public async Task<ConversationSession?> GetAsync(string token, CancellationToken cancellationToken = default)
    {
        var db = _mux.GetDatabase();
        var value = await db.StringGetAsync(Key(token)).ConfigureAwait(false);
        if (value.IsNullOrEmpty) return null;

        try
        {
            return JsonSerializer.Deserialize<ConversationSession>((string)value!, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize session {Token}; treating as missing.", token);
            return null;
        }
    }

    public async Task SaveAsync(string token, ConversationSession session, CancellationToken cancellationToken = default)
    {
        // Mirror InMemoryConversationStore: cap SendHistory at MaxHistoryMessages (keeping system
        // messages) while leaving FullHistory intact for ticket generation. Same canonical helper,
        // so both stores enforce the identical bound.
        var sendHistory = InMemoryConversationStore.TrimHistory(session.SendHistory, _maxHistory);
        var updated = session with { SendHistory = sendHistory, LastActivity = DateTimeOffset.UtcNow };
        var json = JsonSerializer.Serialize(updated, JsonOptions);
        var db = _mux.GetDatabase();
        await db.StringSetAsync(Key(token), json, _ttl).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string token, CancellationToken cancellationToken = default)
    {
        var db = _mux.GetDatabase();
        await db.KeyDeleteAsync(Key(token)).ConfigureAwait(false);
    }

    private static string Key(string token) => $"botwire:session:{token}";
}
