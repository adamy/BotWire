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

using System.ClientModel;
using System.Runtime.CompilerServices;
using BotWire.Core.Abstractions;
using BotWire.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using BotWireChatMessage = BotWire.Core.Models.ChatMessage;

namespace BotWire.Core.Llm;

/// <summary>
/// <see cref="ILlmClient"/> implementation backed by the OpenAI API or any OpenAI-compatible
/// provider (e.g. DeepSeek, Groq). Register via DI using <see cref="OpenAILlmClientOptions"/>.
/// </summary>
public sealed class OpenAILlmClient : ILlmClient
{
    private readonly ChatClient _chatClient;
    private readonly EmbeddingClient _embeddingClient;
    private readonly ILogger<OpenAILlmClient> _logger;
    private readonly bool _hasCustomBaseUrl;
    private readonly string _baseUrl;
    private readonly float? _temperature;

    /// <summary>Initializes a new instance using the provided options.</summary>
    /// <param name="options">Bound options containing API key and model names.</param>
    /// <param name="logger">Logger for runtime diagnostics.</param>
    public OpenAILlmClient(IOptions<OpenAILlmClientOptions> options, ILogger<OpenAILlmClient> logger)
    {
        _logger = logger;
        var opts = options.Value;
        var credential = new ApiKeyCredential(opts.ApiKey);

        _hasCustomBaseUrl = !string.IsNullOrWhiteSpace(opts.BaseUrl);
        _baseUrl = _hasCustomBaseUrl ? opts.BaseUrl! : "https://api.openai.com";
        _temperature = opts.Temperature;

        if (_hasCustomBaseUrl)
        {
            var oaiOptions = new OpenAIClientOptions { Endpoint = new Uri(_baseUrl) };
            var oaiClient = new OpenAIClient(credential, oaiOptions);
            _chatClient = oaiClient.GetChatClient(opts.ChatModel);
            _embeddingClient = oaiClient.GetEmbeddingClient(opts.EmbeddingModel);
        }
        else
        {
            _chatClient = new ChatClient(opts.ChatModel, credential);
            _embeddingClient = new EmbeddingClient(opts.EmbeddingModel, credential);
        }
    }

    /// <inheritdoc/>
    public string Name => "openai";

    /// <inheritdoc/>
    public async Task<string> ChatAsync(
        IReadOnlyList<BotWireChatMessage> messages,
        bool jsonObject = false,
        CancellationToken cancellationToken = default)
    {
        var oaiMessages = MapMessages(messages);
        var options = BuildOptions(jsonObject);
        ChatCompletion completion = await _chatClient.CompleteChatAsync(oaiMessages, options, cancellationToken);
        return completion.Content.Count > 0 ? completion.Content[0].Text : string.Empty;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> ChatStreamingAsync(
        IReadOnlyList<BotWireChatMessage> messages,
        bool jsonObject = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var oaiMessages = MapMessages(messages);
        var options = BuildOptions(jsonObject);
        var stream = _chatClient.CompleteChatStreamingAsync(oaiMessages, options, cancellationToken);

        await foreach (StreamingChatCompletionUpdate update in stream)
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                    yield return part.Text;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_hasCustomBaseUrl)
            _logger.LogWarning(
                "EmbedAsync called against a custom BaseUrl ({BaseUrl}). " +
                "Embedding support varies by provider and may fail.",
                _baseUrl);

        OpenAIEmbedding embedding = await _embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        return embedding.ToFloats().ToArray();
    }

    private ChatCompletionOptions? BuildOptions(bool jsonObject)
    {
        if (!jsonObject && _temperature is null)
            return null;

        var options = new ChatCompletionOptions();
        if (jsonObject)
            options.ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat();
        if (_temperature is { } t)
            options.Temperature = t;
        return options;
    }

    private static OpenAI.Chat.ChatMessage ToOpenAiMessage(BotWireChatMessage msg) =>
        msg.Role switch
        {
            ChatRole.System    => OpenAI.Chat.ChatMessage.CreateSystemMessage(msg.Content),
            ChatRole.User      => OpenAI.Chat.ChatMessage.CreateUserMessage(msg.Content),
            ChatRole.Assistant => OpenAI.Chat.ChatMessage.CreateAssistantMessage(msg.Content),
            _                  => OpenAI.Chat.ChatMessage.CreateUserMessage(msg.Content),
        };

    private static List<OpenAI.Chat.ChatMessage> MapMessages(IReadOnlyList<BotWireChatMessage> messages) =>
        messages.Select(ToOpenAiMessage).ToList();
}
