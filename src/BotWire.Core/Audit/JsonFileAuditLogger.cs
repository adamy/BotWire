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

using System.Text.Encodings.Web;
using System.Text.Json;
using BotWire.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace BotWire.Core.Audit;

/// <summary>
/// Writes audit events as newline-delimited JSON (NDJSON), one file per session bucketed by UTC
/// date: <c>{root}/{yyyyMMdd}/{sessionId}.ndjson</c>. Each line is one JSON object. Files are opened
/// for shared reading so they can be tailed live. Writes are serialised through a semaphore so
/// concurrent <see cref="LogAsync"/> calls never interleave or corrupt a line. Audit failures are
/// logged via <c>ILogger</c> and swallowed: a broken sink must not break a request.
/// </summary>
public sealed class JsonFileAuditLogger : IAuditLogger, IDisposable
{
    private const string NoSessionBucket = "no-session";

    // Relaxed encoder so non-ASCII text (e.g. 中文, emoji) is written as readable UTF-8 instead of
    // \uXXXX escapes. The output is a local NDJSON file, not HTML, so the relaxed escaping is safe.
    private static readonly JsonSerializerOptions _json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private static readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _root;
    private readonly ILogger<JsonFileAuditLogger> _logger;

    /// <summary>Sets the root directory under which dated per-session NDJSON files are written.</summary>
    /// <param name="rootDirectory">
    /// Root folder, absolute or relative to the working directory. Created on demand, along with the
    /// per-day subfolders.
    /// </param>
    /// <param name="logger">Logger for write/serialisation failures.</param>
    public JsonFileAuditLogger(string rootDirectory, ILogger<JsonFileAuditLogger> logger)
    {
        _root = Path.GetFullPath(rootDirectory);
        _logger = logger;
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

        var path = ResolvePath(evt);

        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                // Open-per-write keeps no handle open across date/session boundaries; the gate
                // guarantees a single writer at a time so appends never interleave.
                using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream);
                // Explicit '\n' (not Environment.NewLine) keeps the file valid NDJSON on every platform.
                await writer.WriteAsync(line + '\n').ConfigureAwait(false);
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

    private string ResolvePath(AuditEvent evt)
    {
        var day = evt.Timestamp.UtcDateTime.ToString("yyyyMMdd");
        var session = SanitizeSessionId(evt.SessionId);
        return Path.Combine(_root, day, session + ".ndjson");
    }

    /// <summary>
    /// Maps a session token to a safe file name: characters illegal in a file name (e.g. the
    /// <c>/</c> that appears in base64 tokens) become <c>_</c>; an empty id buckets to a shared file.
    /// </summary>
    private static string SanitizeSessionId(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return NoSessionBucket;

        var chars = sessionId.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(_invalidFileNameChars, chars[i]) >= 0)
                chars[i] = '_';
        }
        return new string(chars);
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
    public void Dispose() => _gate.Dispose();
}
