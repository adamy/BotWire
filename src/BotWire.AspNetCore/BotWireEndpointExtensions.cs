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
using BotWire.Core.Abstractions;
using BotWire.Core.Audit;
using BotWire.Core.Enums;
using BotWire.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Extension methods for registering BotWire endpoints on a route builder.</summary>
public static class BotWireEndpointExtensions
{
    private const string SessionCookieName = "botwire_session";

    private static readonly JsonSerializerOptions _healthJsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Relaxed encoder so non-ASCII text in the SSE stream (e.g. 中文 replies) goes over the wire as
    // readable UTF-8 instead of \uXXXX escapes. Browsers parse both, but raw/console views stay legible.
    private static readonly JsonSerializerOptions _sseJsonOpts = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
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
        BotWireChatService service,
        IOptions<BotWireOptions> options)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (error, token, needsName) = await service.InitSessionAsync(req, ip, context.RequestAborted);
        if (error is not null)
            return Results.Json(
                new ChatResponse(error.Status, error.Message, ""),
                statusCode: error.HttpStatusCode);
        SetSessionCookie(context, token);
        return Results.Json(new InitSessionResponse(token, needsName, options.Value.ErrorMessage));
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
        BotWireChatService service,
        IAuditLogger audit)
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
        var failedOpen        = false;
        var tokensUsed        = 0;
        string? confirmedTicketId = null;
        string? rawResponse       = null;
        string? offTopicMessage   = null;

        try
        {
            await foreach (var evt in service.StreamEventsAsync(prep, context.RequestAborted))
            {
                switch (evt.Kind)
                {
                    case BotEventKind.TextChunk:
                        textBuffer.Append(evt.Text);
                        await WriteSseAsync(response,
                            $"{{\"type\":\"token\",\"value\":{JsonSerializer.Serialize(evt.Text, _sseJsonOpts)}}}");
                        break;

                    case BotEventKind.CollectContact:
                        escalationStarted = true;
                        await WriteSseAsync(response, "{\"type\":\"collect_contact\"}");
                        break;

                    case BotEventKind.TicketConfirmed:
                        confirmedTicketId = evt.TicketId;
                        await WriteSseAsync(response,
                            $"{{\"type\":\"escalated\",\"ticketId\":{JsonSerializer.Serialize(evt.TicketId, _sseJsonOpts)},\"message\":{JsonSerializer.Serialize(evt.ConfirmationMessage, _sseJsonOpts)}}}");
                        break;

                    case BotEventKind.Blocked:
                        // Off-topic turns stream a Blocked event instead of text; keep the message so
                        // the commit can persist and audit it (with the raw JSON + tokens from Done).
                        offTopicMessage = evt.Reason;
                        await WriteSseAsync(response,
                            $"{{\"type\":\"blocked\",\"reason\":{JsonSerializer.Serialize(evt.Reason, _sseJsonOpts)}}}");
                        break;

                    case BotEventKind.Done:
                        failedOpen   = evt.Result?.FailedOpen ?? false;
                        rawResponse  = evt.Result?.RawResponse;
                        tokensUsed  += evt.Result?.TokensUsed ?? 0;
                        await WriteSseAsync(response, "[DONE]");
                        break;

                    case BotEventKind.Usage:
                        // Internal token accounting; not surfaced to the client.
                        tokensUsed += evt.TokensUsed;
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
        catch (Exception ex)
        {
            // Record the failure for the audit trail, then let it propagate (ASP.NET aborts the stream).
            await audit.LogAsync(AuditEvents.Error(prep.Token ?? "", ex.Message), CancellationToken.None);
            throw;
        }

        await service.CommitStreamAsync(
            prep, textBuffer.ToString(), escalationStarted, confirmedTicketId, failedOpen,
            rawResponse, tokensUsed, offTopicMessage, context.RequestAborted);
    }

    // ── GET /botwire/widget.js ──────────────────────────────────────────────────

    private static readonly byte[] _widgetJs = LoadWidgetJs();

    // Content-hash ETag so browsers revalidate cheaply (304) yet pick up a rebuilt widget
    // immediately — a long max-age would otherwise serve a stale bundle for up to an hour.
    private static readonly string _widgetJsETag = ComputeETag(_widgetJs);

    private static byte[] LoadWidgetJs()
    {
        var asm    = typeof(BotWireEndpointExtensions).Assembly;
        using var s  = asm.GetManifestResourceStream("BotWire.AspNetCore.botwire.js")
            ?? throw new InvalidOperationException("BotWire.AspNetCore.botwire.js embedded resource not found.");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static string ComputeETag(byte[] bytes)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return $"\"{Convert.ToHexString(hash, 0, 8)}\"";
    }

    private static async Task HandleWidgetJs(HttpContext context)
    {
        // must-revalidate: cache is allowed but the browser must check the ETag on every use,
        // so a new widget build is served the moment it changes (no hour-long staleness).
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.ETag         = _widgetJsETag;

        if (context.Request.Headers.IfNoneMatch.ToString().Contains(_widgetJsETag))
        {
            context.Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        context.Response.ContentType = "application/javascript; charset=utf-8";
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
