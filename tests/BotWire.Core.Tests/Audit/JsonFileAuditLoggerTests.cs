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
    private readonly string _root;

    public JsonFileAuditLoggerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "botwire-audit-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private JsonFileAuditLogger Create() => new(_root, NullLogger<JsonFileAuditLogger>.Instance);

    private static string Today() => DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyyMMdd");

    private string SessionFile(string sessionId) => Path.Combine(_root, Today(), sessionId + ".ndjson");

    private static List<JsonDocument> ReadLines(string path) =>
        File.ReadAllLines(path)
            .Where(l => l.Length > 0)
            .Select(l => JsonDocument.Parse(l)) // throws if any line is not valid JSON
            .ToList();

    [Fact]
    public async Task LogAsync_WritesToDateFolderPerSessionFile()
    {
        var logger = Create();
        await logger.LogAsync(AuditEvents.UserMessage("sess-123", "refund please"));
        logger.Dispose();

        var path = SessionFile("sess-123");
        Assert.True(File.Exists(path), $"expected audit file at {path}");

        var line = ReadLines(path).Single().RootElement;
        Assert.Equal("message", line.GetProperty("event").GetString());
        Assert.Equal("sess-123", line.GetProperty("sessionId").GetString());
        Assert.Equal("refund please", line.GetProperty("content").GetString());
    }

    [Fact]
    public async Task LogAsync_StoresNonAsciiAsReadableText_NotEscaped()
    {
        var logger = Create();
        await logger.LogAsync(AuditEvents.UserMessage("s", "退款多久到账？"));
        logger.Dispose();

        var raw = File.ReadAllText(SessionFile("s"));
        Assert.Contains("退款多久到账？", raw);
        Assert.DoesNotContain("\\u", raw); // no \uXXXX escapes
    }

    [Fact]
    public async Task LogAsync_SeparateSessions_WriteToSeparateFiles()
    {
        var logger = Create();
        await logger.LogAsync(AuditEvents.UserMessage("alice", "q1"));
        await logger.LogAsync(AuditEvents.UserMessage("bob", "q2"));
        logger.Dispose();

        Assert.True(File.Exists(SessionFile("alice")));
        Assert.True(File.Exists(SessionFile("bob")));
        Assert.Equal("q1", ReadLines(SessionFile("alice")).Single().RootElement.GetProperty("content").GetString());
        Assert.Equal("q2", ReadLines(SessionFile("bob")).Single().RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task LogAsync_SameSession_AppendsOnePerLine()
    {
        var logger = Create();
        await logger.LogAsync(AuditEvents.UserMessage("s", "q"));
        await logger.LogAsync(AuditEvents.AssistantMessage("s", "here is your answer", latencyMs: 42));
        await logger.LogAsync(AuditEvents.Escalated("s", "NEED_HUMAN", "TKT-1"));
        logger.Dispose();

        var lines = ReadLines(SessionFile("s"));
        Assert.Equal(3, lines.Count);
        Assert.Equal("message", lines[0].RootElement.GetProperty("event").GetString());
        Assert.Equal(42, lines[1].RootElement.GetProperty("latencyMs").GetInt64());
        Assert.Equal("TKT-1", lines[2].RootElement.GetProperty("ticketId").GetString());
    }

    [Fact]
    public async Task LogAsync_TokenWithPathChars_IsSanitizedToOneFile()
    {
        var logger = Create();
        // base64 session tokens can contain '/' and '+'
        await logger.LogAsync(AuditEvents.UserMessage("aB/3+x=", "hi"));
        logger.Dispose();

        var dayDir = Path.Combine(_root, Today());
        var file = Assert.Single(Directory.GetFiles(dayDir));
        Assert.DoesNotContain('/', Path.GetFileName(file));
        // sessionId field still holds the original, unsanitised token
        Assert.Equal("aB/3+x=", ReadLines(file).Single().RootElement.GetProperty("sessionId").GetString());
    }

    [Fact]
    public async Task LogAsync_EmptySession_BucketsToSharedFile()
    {
        var logger = Create();
        await logger.LogAsync(AuditEvents.RateLimited("", "MaxRequestsPerIpPerMinute"));
        logger.Dispose();

        Assert.True(File.Exists(SessionFile("no-session")));
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

        var line = ReadLines(SessionFile("s")).Single().RootElement;
        Assert.Equal(eventType, line.GetProperty("event").GetString());
    }

    [Fact]
    public async Task LogAsync_ConcurrentWritesSameSession_ProduceNoCorruption()
    {
        var logger = Create();

        var tasks = Enumerable.Range(0, 200)
            .Select(i => logger.LogAsync(AuditEvents.UserMessage("s", "msg " + i)));
        await Task.WhenAll(tasks);
        logger.Dispose();

        // ReadLines parses every line; a torn/interleaved write would throw here.
        var lines = ReadLines(SessionFile("s"));
        Assert.Equal(200, lines.Count);
        Assert.All(lines, d => Assert.Equal("message", d.RootElement.GetProperty("event").GetString()));
    }
}
