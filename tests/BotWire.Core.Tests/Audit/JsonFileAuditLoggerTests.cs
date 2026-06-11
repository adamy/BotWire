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
using BotWire.Core.Audit;
using Microsoft.Extensions.Logging.Abstractions;

namespace BotWire.Core.Tests.Audit;

public sealed class JsonFileAuditLoggerTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public JsonFileAuditLoggerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "botwire-audit-" + Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_dir, "nested", "audit.ndjson");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private JsonFileAuditLogger Create() => new(_path, NullLogger<JsonFileAuditLogger>.Instance);

    private static List<JsonDocument> ReadLines(string path) =>
        File.ReadAllLines(path)
            .Where(l => l.Length > 0)
            .Select(l => JsonDocument.Parse(l)) // throws if any line is not valid JSON
            .ToList();

    [Fact]
    public async Task LogAsync_CreatesFileAndParentDirectories()
    {
        Assert.False(File.Exists(_path));
        var logger = Create();

        await logger.LogAsync(AuditEvents.UserMessage("s1", "hi"));
        logger.Dispose();

        Assert.True(File.Exists(_path));
    }

    [Fact]
    public async Task LogAsync_WritesEnvelopeAndDataFields()
    {
        var logger = Create();
        await logger.LogAsync(AuditEvents.UserMessage("sess-123", "refund please"));
        logger.Dispose();

        var line = ReadLines(_path).Single().RootElement;
        Assert.Equal("message", line.GetProperty("event").GetString());
        Assert.Equal("sess-123", line.GetProperty("sessionId").GetString());
        Assert.Equal("user", line.GetProperty("role").GetString());
        Assert.Equal("refund please", line.GetProperty("content").GetString());
        Assert.True(line.TryGetProperty("ts", out _));
    }

    [Fact]
    public async Task LogAsync_AppendsOneJsonObjectPerLine()
    {
        var logger = Create();
        await logger.LogAsync(AuditEvents.UserMessage("s", "q"));
        await logger.LogAsync(AuditEvents.AssistantMessage("s", latencyMs: 42));
        await logger.LogAsync(AuditEvents.Escalated("s", "NEED_HUMAN", "TKT-1"));
        logger.Dispose();

        var lines = ReadLines(_path);
        Assert.Equal(3, lines.Count);
        Assert.Equal("message", lines[0].RootElement.GetProperty("event").GetString());
        Assert.Equal(42, lines[1].RootElement.GetProperty("latencyMs").GetInt64());
        Assert.Equal("TKT-1", lines[2].RootElement.GetProperty("ticketId").GetString());
    }

    [Theory]
    [InlineData("message")]
    [InlineData("guard_blocked")]
    [InlineData("escalated")]
    [InlineData("rate_limited")]
    [InlineData("provider_failover")]
    [InlineData("error")]
    public async Task LogAsync_WritesEachEventType(string eventType)
    {
        var logger = Create();
        var evt = eventType switch
        {
            "message" => AuditEvents.UserMessage("s", "hi"),
            "guard_blocked" => AuditEvents.GuardBlocked("s", "pii"),
            "escalated" => AuditEvents.Escalated("s", "NEED_HUMAN", "TKT-9"),
            "rate_limited" => AuditEvents.RateLimited("s", "MaxMessagesPerSession"),
            "provider_failover" => AuditEvents.ProviderFailover("s", "openai", "deepseek", "timeout"),
            _ => AuditEvents.Error("s", "boom"),
        };

        await logger.LogAsync(evt);
        logger.Dispose();

        var line = ReadLines(_path).Single().RootElement;
        Assert.Equal(eventType, line.GetProperty("event").GetString());
    }

    [Fact]
    public async Task LogAsync_ConcurrentWrites_ProduceNoCorruption()
    {
        var logger = Create();

        var tasks = Enumerable.Range(0, 200)
            .Select(i => logger.LogAsync(AuditEvents.UserMessage("s" + i, "msg " + i)));
        await Task.WhenAll(tasks);
        logger.Dispose();

        // ReadLines parses every line; a torn/interleaved write would throw here.
        var lines = ReadLines(_path);
        Assert.Equal(200, lines.Count);
        Assert.All(lines, d => Assert.Equal("message", d.RootElement.GetProperty("event").GetString()));
    }
}
