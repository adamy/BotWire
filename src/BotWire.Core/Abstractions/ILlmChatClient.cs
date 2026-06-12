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

/// <summary>Abstracts chat completion with a large language model provider.</summary>
public interface ILlmChatClient
{
    /// <summary>Human-readable name of this provider, e.g. <c>openai</c>, <c>deepseek</c>.</summary>
    string Name { get; }

    /// <summary>Sends a list of messages to the LLM and returns the complete response with token usage.</summary>
    /// <param name="messages">Ordered conversation history including the latest user message.</param>
    /// <param name="jsonObject">When true, requests a JSON-object response (<c>response_format</c> json_object).</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>The LLM's full response text and the total tokens billed for the call.</returns>
    Task<LlmChatResult> ChatAsync(
        IReadOnlyList<ChatMessage> messages, bool jsonObject = false, CancellationToken cancellationToken = default);

    /// <summary>Sends a list of messages to the LLM and streams the response token by token.</summary>
    /// <param name="messages">Ordered conversation history including the latest user message.</param>
    /// <param name="jsonObject">When true, requests a JSON-object response (<c>response_format</c> json_object).</param>
    /// <param name="onUsage">
    /// Invoked once at the end of the stream with the total tokens billed for the call. Providers that
    /// do not report streaming usage supply an estimate. May be skipped if the stream faults early.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the stream.</param>
    /// <returns>An async sequence of text tokens as they are produced by the LLM.</returns>
    IAsyncEnumerable<string> ChatStreamingAsync(
        IReadOnlyList<ChatMessage> messages,
        bool jsonObject = false,
        Action<int>? onUsage = null,
        CancellationToken cancellationToken = default);
}
