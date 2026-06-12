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

using System.ComponentModel.DataAnnotations;

namespace BotWire.Core.Llm;

/// <summary>Configuration options for <see cref="OpenAILlmClient"/>.</summary>
public sealed class OpenAILlmClientOptions
{
    /// <summary>OpenAI API key. Required.</summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Chat completion model identifier. Defaults to <c>gpt-4o-mini</c>.</summary>
    public string ChatModel { get; set; } = "gpt-4o-mini";

    /// <summary>Embedding model identifier. Defaults to <c>text-embedding-3-small</c>.</summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Optional base URL for OpenAI-compatible APIs, e.g. <c>https://api.deepseek.com/v1</c>.
    /// When <see langword="null"/>, the default OpenAI endpoint is used.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Sampling temperature for chat completions. Defaults to <c>0.2</c> (low, for consistent
    /// grounded answers). When <see langword="null"/>, the parameter is omitted and the provider's
    /// own default applies.
    /// </summary>
    public float? Temperature { get; set; } = 0.2f;
}
