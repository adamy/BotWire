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

using System.Text;
using BotWire.AspNetCore;
using BotWire.Channels.Email;
using RedisShop.Api;

// Render non-ASCII log output correctly instead of "??" in the console.
Console.OutputEncoding = Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

var apiKey  = Environment.GetEnvironmentVariable("BOTWIRE_TEST_API_KEY")
    ?? throw new InvalidOperationException("BOTWIRE_TEST_API_KEY environment variable is not set.");
var model   = Environment.GetEnvironmentVariable("BOTWIRE_TEST_MODEL")    ?? "gpt-4o-mini";
var baseUrl = Environment.GetEnvironmentVariable("BOTWIRE_TEST_BASE_URL");
var redis   = Environment.GetEnvironmentVariable("BOTWIRE_REDIS")          ?? "localhost:6379";

// Vite dev server origin — the React shopfront calls the BotWire endpoints cross-origin.
var webOrigin = Environment.GetEnvironmentVariable("SHOP_WEB_ORIGIN") ?? "http://localhost:5173";

builder.Services.AddBotWire(opts =>
{
    opts.TopicDescription = "Customer support for Acme Store";
    opts.Documents        = [Path.Combine(AppContext.BaseDirectory, "docs", "faq.md")];
    opts.ChatProvider     = new OpenAIProviderOptions
    {
        ApiKey  = apiKey,
        Model   = model,
        BaseUrl = baseUrl,
    };

    // Tight rate-limit caps so the Redis-backed counters are easy to observe/test.
    opts.RateLimiting.MaxMessagesPerMinute    = 5;
    opts.RateLimiting.MaxSessionsPerIpPerHour = 50;
    opts.RateLimiting.DailyTokenBudget        = 200_000;

    // The React dev server lives on a different origin; allow it explicitly.
    opts.Cors.AllowedOrigins.Add(webOrigin);

    // Escalation tickets are delivered by email. Points at Mailpit (localhost:1025) like
    // the BasicEmail sample — run `docker run -d -p 1025:1025 -p 8025:8025 axllent/mailpit`
    // and view captured tickets at http://localhost:8025.
    opts.Email = new EmailOptions
    {
        SmtpHost    = "localhost",
        Port        = 1025,
        UseSsl      = false,
        FromAddress = "support@acme.example",
        ToAddress   = "support-team@acme.example",
    };
})
// Redis-backed conversation store + distributed rate-limit counters (Tasks 33 & 34).
// Requires Redis reachable at BOTWIRE_REDIS (default localhost:6379 — e.g. Docker Desktop).
.AddBotWireRedis(redis)
.AddJsonAuditLog(Path.Combine(AppContext.BaseDirectory, "logs", "audit"));

// CORS for the sample product API (separate from the "botwire" policy).
builder.Services.AddCors(o => o.AddPolicy("shop", p =>
    p.WithOrigins(webOrigin).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();

// ── Sample product catalog ───────────────────────────────────────────────────
app.MapGet("/api/products", () => Catalog.Products).RequireCors("shop");
app.MapGet("/api/products/{id}", (string id) =>
    Catalog.Products.FirstOrDefault(p => p.Id == id) is { } product
        ? Results.Ok(product)
        : Results.NotFound()).RequireCors("shop");

// ── BotWire support endpoints (/support/*) ───────────────────────────────────
app.MapBotWire();

app.Run();
