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

using BotWire.AspNetCore;
using BotWire.Core.Abstractions;
using BotWire.Core.Exceptions;
using BotWire.Core.Llm;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>DI registration helpers for the full BotWire support-bot stack.</summary>
public static class BotWireServiceCollectionExtensions
{
    /// <summary>
    /// Registers all BotWire services: LLM client, conversation store, RAG answer provider,
    /// session tokens, PII guard, rate limiter, optional email channel, CORS policy, and the
    /// startup validation filter.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Delegate to populate <see cref="BotWireOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddBotWire(
        this IServiceCollection services,
        Action<BotWireOptions> configure)
    {
        // Peek: apply configure once now so structural registration decisions (email channel,
        // CORS policy) can be made at service-registration time rather than deferred to resolve
        // time. The same delegate is passed to AddOptions below and runs again on first resolve.
        var opts = new BotWireOptions();
        configure(opts);

        // ValidateDataAnnotations catches [Required] violations (e.g. TopicDescription missing).
        // ValidateOnStart is intentionally omitted: the BotWireStartupFilter reads _options.Value
        // during app build, which triggers DataAnnotations validation at the same startup point.
        services.AddOptions<BotWireOptions>()
            .Configure(configure)
            .ValidateDataAnnotations();

        // ── LLM client ──────────────────────────────────────────────────────────
        // OpenAILlmClientOptions is configured from BotWireOptions at resolve time.
        // A single OpenAILlmClient handles both chat and (if configured) embedding.
        services.AddOptions<OpenAILlmClientOptions>()
            .Configure<IOptions<BotWireOptions>>((llmOpts, bwOpts) =>
            {
                var bw = bwOpts.Value;
                if (bw.ChatProvider is null) return; // startup filter gives a clear error
                llmOpts.ApiKey         = bw.ChatProvider.ApiKey;
                llmOpts.ChatModel      = bw.ChatProvider.Model;
                llmOpts.EmbeddingModel = bw.EmbeddingProvider?.Model ?? "text-embedding-3-small";
                llmOpts.BaseUrl        = bw.ChatProvider.BaseUrl;
            });

        services.AddSingleton<OpenAILlmClient>();
        services.AddSingleton<ILlmChatClient>(sp => sp.GetRequiredService<OpenAILlmClient>());
        services.AddSingleton<ILlmClient>(sp => sp.GetRequiredService<OpenAILlmClient>());

        // ILlmEmbedClient is registered but throws a descriptive exception if resolved without
        // EmbeddingProvider — embedding-based retrieval is opt-in in Phase 1.
        services.AddSingleton<ILlmEmbedClient>(sp =>
        {
            var bw = sp.GetRequiredService<IOptions<BotWireOptions>>().Value;
            if (bw.EmbeddingProvider is null)
                throw new BotWireConfigurationException(
                    "BotWireOptions.EmbeddingProvider is not configured. " +
                    "Set EmbeddingProvider before enabling embedding-based retrieval.");
            return sp.GetRequiredService<OpenAILlmClient>();
        });

        // ── Guard (PII + rate limiter) ──────────────────────────────────────────
        services.AddBotWireGuard(
            configureRateLimit: o => o.MaxRequestsPerIpPerMinute = opts.MaxRequestsPerIpPerMinute);

        // ── Conversation store ──────────────────────────────────────────────────
        services.AddInMemoryConversationStore(o => o.SessionTtl = opts.SessionTtl);

        // ── Answer provider (RAG, Mode A) ───────────────────────────────────────
        services.AddBotWireAnswerProvider(o =>
        {
            o.DocumentPaths        = [.. opts.Documents];
            o.SystemPromptPreamble = opts.TopicDescription;
        });

        // ── Session tokens ──────────────────────────────────────────────────────
        services.AddBotWireSessionTokenService();

        // ── Email channel (optional) ────────────────────────────────────────────
        if (opts.Email is not null)
        {
            var email = opts.Email;
            services.AddBotWireEmail(o =>
            {
                o.SmtpHost    = email.SmtpHost;
                o.Port        = email.Port;
                o.UseSsl      = email.UseSsl;
                o.FromAddress = email.FromAddress;
                o.ToAddress   = email.ToAddress;
                o.Username    = email.Username;
                o.Password    = email.Password;
                o.FromName    = email.FromName;
            });
        }

        // ── CORS policy "botwire" ───────────────────────────────────────────────
        // AllowAnyOrigin and AllowCredentials cannot be combined; when origins are
        // unrestricted we omit credentials to stay within the CORS spec.
        services.AddCors(cors => cors.AddPolicy("botwire", policy =>
        {
            var origins = opts.Cors.AllowedOrigins;
            if (origins.Count == 0)
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            else
                policy.WithOrigins([.. origins]).AllowAnyHeader().AllowAnyMethod();
        }));

        // ── Startup validation ──────────────────────────────────────────────────
        services.AddTransient<IStartupFilter, BotWireStartupFilter>();

        return services;
    }
}
