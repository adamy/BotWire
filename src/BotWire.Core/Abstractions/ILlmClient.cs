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

using BotWire.Core.Models;

namespace BotWire.Core.Abstractions;

/// <summary>Abstracts communication with a large language model provider.</summary>
public interface ILlmClient
{
    /// <summary>Human-readable name of this LLM provider, e.g. <c>OpenAI</c>.</summary>
    string Name { get; }

    /// <summary>Indicates whether this client supports generating text embeddings.</summary>
    bool SupportsEmbedding { get; }

    /// <summary>Sends a list of messages to the LLM and returns the complete response.</summary>
    /// <param name="messages">Ordered conversation history including the latest user message.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>The LLM's full response text.</returns>
    Task<string> ChatAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>Sends a list of messages to the LLM and streams the response token by token.</summary>
    /// <param name="messages">Ordered conversation history including the latest user message.</param>
    /// <param name="cancellationToken">Token to cancel the stream.</param>
    /// <returns>An async sequence of text tokens as they are produced by the LLM.</returns>
    IAsyncEnumerable<string> ChatStreamingAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>Generates a vector embedding for the given text.</summary>
    /// <param name="text">The input text to embed.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>A floating-point embedding vector.</returns>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
