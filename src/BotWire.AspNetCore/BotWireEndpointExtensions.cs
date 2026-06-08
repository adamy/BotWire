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

using System.Reflection;
using System.Text;
using System.Text.Json;
using BotWire.AspNetCore;
using BotWire.Core.Enums;
using BotWire.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Extension methods for registering BotWire endpoints on a route builder.</summary>
public static class BotWireEndpointExtensions
{
    private const string SessionCookieName = "botwire_session";

    private static readonly JsonSerializerOptions _healthJsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Maps all BotWire endpoints (<c>POST /support/chat</c>, <c>POST /support/chat/stream</c>,
    /// <c>GET /botwire/widget.js</c>, <c>GET /botwire/health</c>) and applies the
    /// <c>"botwire"</c> CORS policy to each.
    /// </summary>
    public static IEndpointRouteBuilder MapBotWire(this IEndpointRouteBuilder app)
    {
        app.MapPost("/support/session",     HandleInitSessionAsync) .RequireCors("botwire");
        app.MapPost("/support/chat",        HandleChatAsync)        .RequireCors("botwire");
        app.MapPost("/support/chat/stream", HandleChatStreamAsync)  .RequireCors("botwire");
        app.MapGet( "/botwire/widget.js",   HandleWidgetJs)         .RequireCors("botwire");
        app.MapGet( "/botwire/health",      HandleHealth)           .RequireCors("botwire");
        return app;
    }

    // ── POST /support/session ───────────────────────────────────────────────────

    private static async Task<IResult> HandleInitSessionAsync(
        InitSessionRequest req,
        HttpContext context,
        BotWireChatService service)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (error, token, needsName) = await service.InitSessionAsync(req, ip, context.RequestAborted);
        if (error is not null)
            return Results.Json(
                new ChatResponse(error.Status, error.Message, ""),
                statusCode: error.HttpStatusCode);
        SetSessionCookie(context, token);
        return Results.Json(new InitSessionResponse(token, needsName));
    }

    // ── POST /support/chat ──────────────────────────────────────────────────────

    private static async Task<IResult> HandleChatAsync(
        ChatRequest req,
        HttpContext context,
        BotWireChatService service)
    {
        var ip     = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await service.AnswerAsync(req, ip, context.RequestAborted);

        if (result.HttpStatusCode == 200)
            SetSessionCookie(context, result.SessionToken);

        return Results.Json(
            new ChatResponse(result.Status, result.Message, result.SessionToken, result.TicketId),
            statusCode: result.HttpStatusCode == 200 ? null : result.HttpStatusCode);
    }

    // ── POST /support/chat/stream ───────────────────────────────────────────────

    private static async Task HandleChatStreamAsync(
        ChatRequest req,
        HttpContext context,
        BotWireChatService service)
    {
        var ip       = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var response = context.Response;

        var prep = await service.PrepareStreamAsync(req, ip, context.RequestAborted);
        if (prep.Error is { } error)
        {
            response.StatusCode = error.HttpStatusCode;
            await response.WriteAsJsonAsync(
                new ChatResponse(error.Status, error.Message, error.SessionToken));
            return;
        }

        // SSE headers and session cookie must be committed before the first body byte
        response.ContentType                  = "text/event-stream";
        response.Headers["Cache-Control"]     = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";
        SetSessionCookie(context, prep.Token!);

        var textBuffer        = new StringBuilder();
        var escalationStarted = false;
        string? confirmedTicketId = null;

        try
        {
            await foreach (var evt in service.StreamEventsAsync(prep, context.RequestAborted))
            {
                switch (evt.Kind)
                {
                    case BotEventKind.TextChunk:
                        textBuffer.Append(evt.Text);
                        await WriteSseAsync(response,
                            $"{{\"type\":\"token\",\"value\":{JsonSerializer.Serialize(evt.Text)}}}");
                        break;

                    case BotEventKind.CollectContact:
                        escalationStarted = true;
                        await WriteSseAsync(response, "{\"type\":\"collect_contact\"}");
                        break;

                    case BotEventKind.TicketConfirmed:
                        confirmedTicketId = evt.TicketId;
                        await WriteSseAsync(response,
                            $"{{\"type\":\"escalated\",\"ticketId\":{JsonSerializer.Serialize(evt.TicketId)}}}");
                        break;

                    case BotEventKind.Blocked:
                        await WriteSseAsync(response,
                            $"{{\"type\":\"blocked\",\"reason\":{JsonSerializer.Serialize(evt.Reason)}}}");
                        break;

                    case BotEventKind.Done:
                        await WriteSseAsync(response, "[DONE]");
                        break;

                    case BotEventKind.Escalated:
                        // CollectContact follows immediately; no SSE event emitted here
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — skip CommitStreamAsync to avoid storing a partial turn
            return;
        }

        await service.CommitStreamAsync(
            prep, textBuffer.ToString(), escalationStarted, confirmedTicketId, context.RequestAborted);
    }

    // ── GET /botwire/widget.js ──────────────────────────────────────────────────

    private static readonly byte[] _widgetJs = LoadWidgetJs();

    private static byte[] LoadWidgetJs()
    {
        var asm    = typeof(BotWireEndpointExtensions).Assembly;
        using var s  = asm.GetManifestResourceStream("BotWire.AspNetCore.botwire.js")
            ?? throw new InvalidOperationException("BotWire.AspNetCore.botwire.js embedded resource not found.");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static async Task HandleWidgetJs(HttpContext context)
    {
        context.Response.ContentType              = "application/javascript; charset=utf-8";
        context.Response.Headers.CacheControl = "public, max-age=3600";
        await context.Response.Body.WriteAsync(_widgetJs);
    }

    // ── GET /botwire/health ─────────────────────────────────────────────────────

    private static IResult HandleHealth()
    {
        var version = typeof(BotWireEndpointExtensions).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";
        return Results.Json(new { Status = "ok", Version = version }, _healthJsonOpts);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static async Task WriteSseAsync(HttpResponse response, string data)
    {
        await response.WriteAsync($"data: {data}\n\n");
        await response.Body.FlushAsync();
    }

    private static void SetSessionCookie(HttpContext context, string token)
    {
        context.Response.Cookies.Append(SessionCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure   = context.Request.IsHttps,
        });
    }
}
