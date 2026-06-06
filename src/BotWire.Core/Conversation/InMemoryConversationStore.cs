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

using System.Collections.Concurrent;
using BotWire.Core.Abstractions;
using BotWire.Core.Enums;
using BotWire.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotWire.Core.Conversation;

/// <summary>
/// In-process <see cref="IConversationStore"/> backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// A self-owned background timer periodically evicts sessions that have been idle longer than
/// <see cref="ConversationStoreOptions.SessionTtl"/>. Suitable for single-instance deployments;
/// state is lost on restart and is not shared across processes.
/// </summary>
public sealed class InMemoryConversationStore : IConversationStore, IDisposable
{
    private readonly ConcurrentDictionary<string, ConversationSession> _sessions = new();
    private readonly ConversationStoreOptions _options;
    private readonly ILogger<InMemoryConversationStore> _logger;
    private readonly Timer? _cleanupTimer;

    /// <summary>Initializes the store and starts the background TTL cleanup sweep.</summary>
    /// <param name="options">Bound store options (TTL and history cap).</param>
    /// <param name="logger">Logger for cleanup diagnostics.</param>
    public InMemoryConversationStore(
        IOptions<ConversationStoreOptions> options,
        ILogger<InMemoryConversationStore> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (_options.SessionTtl > TimeSpan.Zero)
            _cleanupTimer = new Timer(_ => Cleanup(), null, _options.SessionTtl, _options.SessionTtl);
    }

    /// <inheritdoc/>
    public Task<ConversationSession?> GetAsync(string token, CancellationToken cancellationToken = default)
    {
        _sessions.TryGetValue(token, out var session);
        return Task.FromResult(session);
    }

    /// <inheritdoc/>
    public Task SaveAsync(string token, ConversationSession session, CancellationToken cancellationToken = default)
    {
        var history = TrimHistory(session.History, _options.MaxHistoryMessages);
        var updated = session with { History = history, LastActivity = DateTimeOffset.UtcNow };
        _sessions[token] = updated;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string token, CancellationToken cancellationToken = default)
    {
        _sessions.TryRemove(token, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes every session idle longer than the configured TTL, measured against
    /// <paramref name="now"/>. Exposed internally so tests can drive eviction deterministically
    /// without waiting on the timer. Returns the number of sessions removed.
    /// </summary>
    internal int RemoveExpired(DateTimeOffset now)
    {
        var removed = 0;
        foreach (var entry in _sessions)
        {
            if (now - entry.Value.LastActivity <= _options.SessionTtl)
                continue;

            // Compare-and-remove: only evict if the snapshot still matches, so a session
            // saved concurrently after this read is not lost.
            if (_sessions.TryRemove(entry))
                removed++;
        }

        return removed;
    }

    /// <summary>
    /// Keeps all system messages and the newest non-system messages, preserving original ordering.
    /// System messages are never dropped; if they alone exceed <paramref name="max"/>, non-system
    /// messages are still trimmed to zero but the result may contain more than <paramref name="max"/>
    /// entries. When all messages are system and no non-system exist, the list is returned unchanged.
    /// </summary>
    internal static List<ChatMessage> TrimHistory(List<ChatMessage> history, int max)
    {
        if (max <= 0 || history.Count <= max)
            return history;

        var systemCount = 0;
        foreach (var message in history)
        {
            if (message.Role == ChatRole.System)
                systemCount++;
        }

        var nonSystemCount = history.Count - systemCount;
        var keepNonSystem = Math.Max(0, max - systemCount);
        var dropCount = nonSystemCount - keepNonSystem;
        if (dropCount <= 0)
            return history;

        var result = new List<ChatMessage>(history.Count - dropCount);
        var dropped = 0;
        foreach (var message in history)
        {
            if (message.Role != ChatRole.System && dropped < dropCount)
            {
                dropped++;
                continue;
            }

            result.Add(message);
        }

        return result;
    }

    private void Cleanup()
    {
        try
        {
            var removed = RemoveExpired(DateTimeOffset.UtcNow);
            if (removed > 0)
                _logger.LogDebug("Evicted {Count} expired conversation session(s).", removed);
        }
        catch (Exception ex)
        {
            // A timer callback must never throw; an unobserved exception would crash the process.
            _logger.LogError(ex, "Conversation store cleanup sweep failed.");
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _cleanupTimer?.Dispose();
}
