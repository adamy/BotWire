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

namespace BotWire.AspNetCore;

/// <summary>Configuration for a single OpenAI or OpenAI-compatible provider endpoint.</summary>
public sealed class OpenAIProviderOptions
{
    /// <summary>API key for the provider.</summary>
    [Required]
    public string ApiKey { get; set; } = "";

    /// <summary>Model identifier, e.g. <c>gpt-4o</c> or <c>text-embedding-3-small</c>.</summary>
    [Required]
    public string Model { get; set; } = "";

    /// <summary>
    /// Optional base URL for OpenAI-compatible endpoints, e.g. <c>https://api.deepseek.com/v1</c>.
    /// When <see langword="null"/>, the default OpenAI endpoint is used.
    /// </summary>
    public string? BaseUrl { get; set; }
}
