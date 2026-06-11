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
using BotWire.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace BotWire.Core.Audit;

/// <summary>
/// Appends audit events to a newline-delimited JSON (NDJSON) file — one JSON object per line.
/// The file is opened for shared reading so it can be tailed live. Writes are serialised through a
/// semaphore so concurrent <see cref="LogAsync"/> calls never interleave or corrupt a line.
/// Audit failures are logged via <c>ILogger</c> and swallowed: a broken sink must not break a request.
/// </summary>
public sealed class JsonFileAuditLogger : IAuditLogger, IDisposable
{
    private static readonly JsonSerializerOptions _json = new();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly StreamWriter _writer;
    private readonly ILogger<JsonFileAuditLogger> _logger;

    /// <summary>Opens (creating it and any parent directories if needed) the NDJSON file for append.</summary>
    /// <param name="path">Destination file path, absolute or relative to the working directory.</param>
    /// <param name="logger">Logger for write/serialisation failures.</param>
    public JsonFileAuditLogger(string path, ILogger<JsonFileAuditLogger> logger)
    {
        _logger = logger;

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    }

    /// <inheritdoc/>
    public async Task LogAsync(AuditEvent evt, CancellationToken cancellationToken = default)
    {
        string line;
        try
        {
            line = Serialize(evt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BotWire: failed to serialise audit event '{Event}'.", evt.EventType);
            return;
        }

        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Explicit '\n' (not Environment.NewLine) keeps the file valid NDJSON on every platform.
                await _writer.WriteAsync(line + '\n').ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            // Never propagate: a failed audit write must not break the request it describes.
            _logger.LogError(ex, "BotWire: failed to write audit event '{Event}'.", evt.EventType);
        }
    }

    private static string Serialize(AuditEvent evt)
    {
        // Envelope fields first, then the event-specific data flattened alongside them.
        var record = new Dictionary<string, object?>(evt.Data.Count + 3)
        {
            ["ts"] = evt.Timestamp,
            ["event"] = evt.EventType,
            ["sessionId"] = evt.SessionId,
        };
        foreach (var kv in evt.Data)
            record[kv.Key] = kv.Value;

        return JsonSerializer.Serialize(record, _json);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _writer.Dispose();
        _gate.Dispose();
    }
}
