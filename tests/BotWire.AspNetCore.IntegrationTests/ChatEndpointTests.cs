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

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BotWire.Channels.Email;

namespace BotWire.AspNetCore.IntegrationTests;

public class ChatEndpointTests
{
    // ── LLM tests (require BOTWIRE_TEST_API_KEY) ────────────────────────────────

    [SkipIfNoApiKeyFact]
    public async Task OnTopic_ReturnsAnswered()
    {
        await using var host = await BotWireTestHost.CreateAsync();
        var resp = await host.Client.PostAsJsonAsync("/support/chat",
            new ChatRequest { Message = "How do I reset my password?" });
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.Equal("Answered", body!.Status);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));
        Assert.False(string.IsNullOrWhiteSpace(body.SessionToken));
    }

    [SkipIfNoApiKeyFact]
    public async Task OffTopic_ReturnsAnswered()
    {
        // Off-topic messages → ANSWER (politely redirect), not ESCALATE.
        // ESCALATE is reserved for support issues requiring human account/order access.
        await using var host = await BotWireTestHost.CreateAsync();
        var resp = await host.Client.PostAsJsonAsync("/support/chat",
            new ChatRequest { Message = "What is the current stock price of Apple?" });
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.Equal("Answered", body!.Status);
    }

    /// <summary>
    /// Happy-path multi-turn conversation: 5 user messages about store location and
    /// opening hours, all answered from the store-info knowledge-base document.
    /// Session token is carried across all turns to exercise conversation continuity.
    /// </summary>
    [SkipIfNoApiKeyFact]
    public async Task MultiTurn_StoreInfo_AllAnswered()
    {
        await using var host = await BotWireTestHost.CreateAsync(
            documents: ["support-faq.md", "store-info.md"]);

        string? token = null;

        async Task<ChatResponse> SendAsync(string message)
        {
            var resp = await host.Client.PostAsJsonAsync("/support/chat",
                new ChatRequest { Message = message, SessionToken = token });
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<ChatResponse>();
            token = body!.SessionToken;
            return body;
        }

        // Turn 1: where is the shop?
        var t1 = await SendAsync("Where is your shop located?");
        Assert.Equal("Answered", t1.Status);
        Assert.False(string.IsNullOrWhiteSpace(t1.Message));

        // Turn 2: general opening hours
        var t2 = await SendAsync("What are your opening hours?");
        Assert.Equal("Answered", t2.Status);
        Assert.False(string.IsNullOrWhiteSpace(t2.Message));

        // Turn 3: specific weekend hours
        var t3 = await SendAsync("Are you open on Saturdays?");
        Assert.Equal("Answered", t3.Status);

        // Turn 4: Sunday closing time
        var t4 = await SendAsync("What time do you close on Sundays?");
        Assert.Equal("Answered", t4.Status);

        // Turn 5: parking
        var t5 = await SendAsync("Is there parking near the store?");
        Assert.Equal("Answered", t5.Status);
        Assert.False(string.IsNullOrWhiteSpace(t5.Message));

        // Session token must be stable across all turns
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [SkipIfNoMailpitFact]
    [Trait("Category", "RequiresMailpit")]
    public async Task FullEscalation_CreatesTicketAndSendsEmail()
    {
        await using var host = await BotWireTestHost.CreateAsync(extra: opts =>
        {
            opts.Email = new EmailOptions
            {
                SmtpHost    = "localhost",
                Port        = 1025,
                UseSsl      = false,   // Mailpit: plain SMTP, no TLS
                FromAddress = "bot@botwire.io",
                ToAddress   = "support@botwire.io",
            };
        });

        // Turn 1: send off-topic message → NeedHuman
        var r1 = await host.Client.PostAsJsonAsync("/support/chat",
            new ChatRequest { Message = "What time does your store open on Sundays?" });
        r1.EnsureSuccessStatusCode();
        var b1 = await r1.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.Equal("NeedHuman", b1!.Status);

        // Turn 2: provide contact email → ticket created
        var r2 = await host.Client.PostAsJsonAsync("/support/chat",
            new ChatRequest
            {
                Message      = "Please help me.",
                SessionToken = b1.SessionToken,
                ContactEmail = "user@example.com",
            });
        r2.EnsureSuccessStatusCode();
        var b2 = await r2.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.Equal("TicketCreated", b2!.Status);
        Assert.Matches(@"TKT-\d{8}-\d+", b2.TicketId!);

        // Verify Mailpit received at least one email
        using var mailpit = new HttpClient { BaseAddress = new Uri("http://localhost:8025") };
        var mailpitJson = await mailpit.GetFromJsonAsync<JsonDocument>("/api/v1/messages");
        var total = mailpitJson!.RootElement.GetProperty("total").GetInt32();
        Assert.True(total > 0, "Expected at least one email in Mailpit.");
    }

    // ── Guard tests (no API key required) ────────────────────────────────────────

    [Fact]
    public async Task RateLimit_Returns429()
    {
        // Use /support/session (no LLM call) so this test runs without an API key.
        // maxRpm=3: first 3 allowed, 4th rate-limited.
        await using var host = await BotWireTestHost.CreateAsync(maxRpm: 3);
        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++)
        {
            last = await host.Client.PostAsJsonAsync("/support/session",
                new InitSessionRequest());
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }

    [Fact]
    public async Task PiiGuard_Returns400()
    {
        await using var host = await BotWireTestHost.CreateAsync();
        var resp = await host.Client.PostAsJsonAsync("/support/chat",
            new ChatRequest { Message = "Please email me at attacker@evil.com" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.Equal("Blocked", body!.Status);
    }

    [Fact]
    public async Task InitSession_ReturnsToken()
    {
        await using var host = await BotWireTestHost.CreateAsync();
        var resp = await host.Client.PostAsJsonAsync("/support/session",
            new InitSessionRequest());
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<InitSessionResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.SessionToken));
        Assert.True(body.NeedsName);
    }

    [Fact]
    public async Task InvalidToken_Returns400()
    {
        await using var host = await BotWireTestHost.CreateAsync();
        var resp = await host.Client.PostAsJsonAsync("/support/chat",
            new ChatRequest { Message = "Hello", SessionToken = "invalid-garbage-token" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.Equal("Blocked", body!.Status);
    }
}
