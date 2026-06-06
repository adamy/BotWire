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

using System.Runtime.CompilerServices;
using System.Text;
using BotWire.Core.Abstractions;
using BotWire.Core.Enums;
using BotWire.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotWire.Core.Rag;

/// <summary>
/// RAG Mode A answer provider: grounds an <see cref="ILlmChatClient"/> in a fixed document set and
/// uses a first-line ANSWER / ESCALATE control word to decide whether the bot answered or must hand
/// off to a human. The system prompt is assembled (and its token budget enforced) on first use.
/// </summary>
public sealed class AnswerProvider : IAnswerProvider
{
    private readonly ILlmChatClient _chat;
    private readonly IDocumentLoader _loader;
    private readonly AnswerProviderOptions _options;
    private readonly ILogger<AnswerProvider> _logger;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private string? _systemPrompt;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="chat">The chat LLM used to generate answers.</param>
    /// <param name="loader">Loader for the knowledge-base documents.</param>
    /// <param name="options">Bound provider options (document paths, preamble).</param>
    /// <param name="logger">Logger for fail-open diagnostics.</param>
    public AnswerProvider(
        ILlmChatClient chat,
        IDocumentLoader loader,
        IOptions<AnswerProviderOptions> options,
        ILogger<AnswerProvider> logger)
    {
        _chat = chat;
        _loader = loader;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AnswerResult> AnswerAsync(
        string message,
        ConversationSession session,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = await GetSystemPromptAsync(cancellationToken);
        var messages = BuildMessages(systemPrompt, session, message);

        var raw = await _chat.ChatAsync(messages, cancellationToken);
        var parsed = ResponseControl.Parse(raw);

        if (!parsed.Recognized)
            _logger.LogWarning("LLM response had no recognized control word; failing open as ANSWER.");

        return new AnswerResult(parsed.Status, parsed.Message);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<BotEvent> StreamAsync(
        string message,
        ConversationSession session,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var systemPrompt = await GetSystemPromptAsync(cancellationToken);
        var messages = BuildMessages(systemPrompt, session, message);

        var buffer = new StringBuilder();
        var answer = new StringBuilder();
        var resolved = false; // once true, the control-word decision is made and deltas pass through

        await foreach (var delta in _chat.ChatStreamingAsync(messages, cancellationToken))
        {
            if (resolved)
            {
                answer.Append(delta);
                yield return BotEvent.TextChunk(delta);
                continue;
            }

            buffer.Append(delta);
            var text = buffer.ToString();
            var newline = text.IndexOf('\n');

            if (newline >= 0)
            {
                var prefix = text[..newline].Trim();

                if (ResponseControl.StartsWith(prefix, ResponseControl.Escalate))
                {
                    yield return BotEvent.Escalated();
                    yield break;
                }

                resolved = true;
                string emit;
                if (ResponseControl.StartsWith(prefix, ResponseControl.Answer))
                {
                    emit = ResponseControl.Body(text, newline, ResponseControl.Answer);
                }
                else
                {
                    _logger.LogWarning("Stream prefix had no recognized control word; failing open as ANSWER.");
                    emit = text;
                }

                EmitDelta(emit, answer);
                if (emit.Length > 0)
                    yield return BotEvent.TextChunk(emit);

                continue;
            }

            if (buffer.Length >= ResponseControl.ScanLimit)
            {
                _logger.LogWarning(
                    "No control word within {Limit} chars; failing open as ANSWER.", ResponseControl.ScanLimit);
                resolved = true;
                var emit = text;
                EmitDelta(emit, answer);
                yield return BotEvent.TextChunk(emit);
            }
        }

        // Stream ended before the control word was resolved: a short reply with no trailing newline.
        if (!resolved)
        {
            var text = buffer.ToString();
            var trimmed = text.TrimStart();

            if (ResponseControl.StartsWith(trimmed, ResponseControl.Escalate))
            {
                yield return BotEvent.Escalated();
                yield break;
            }

            string emit;
            if (ResponseControl.StartsWith(trimmed, ResponseControl.Answer))
            {
                emit = ResponseControl.Body(text, -1, ResponseControl.Answer);
            }
            else
            {
                if (text.Length > 0)
                    _logger.LogWarning("Stream ended with no recognized control word; failing open as ANSWER.");
                emit = text;
            }

            EmitDelta(emit, answer);
            if (emit.Length > 0)
                yield return BotEvent.TextChunk(emit);
        }

        yield return BotEvent.Done(new AnswerResult(AnswerStatus.Answered, answer.ToString()));
    }

    private static void EmitDelta(string delta, StringBuilder answer)
    {
        if (delta.Length > 0)
            answer.Append(delta);
    }

    private static List<ChatMessage> BuildMessages(
        string systemPrompt,
        ConversationSession session,
        string userMessage)
    {
        var messages = new List<ChatMessage>(session.History.Count + 2)
        {
            new(ChatRole.System, systemPrompt),
        };
        messages.AddRange(session.History);
        messages.Add(new ChatMessage(ChatRole.User, userMessage));
        return messages;
    }

    private async Task<string> GetSystemPromptAsync(CancellationToken cancellationToken)
    {
        if (_systemPrompt is not null)
            return _systemPrompt;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_systemPrompt is null)
            {
                var documents = await _loader.LoadAsync([.. _options.DocumentPaths], cancellationToken);
                _systemPrompt = SystemPromptBuilder.Build(documents, _options.SystemPromptPreamble);
            }
        }
        finally
        {
            _initLock.Release();
        }

        return _systemPrompt;
    }
}
