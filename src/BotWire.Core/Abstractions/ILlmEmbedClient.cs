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

namespace BotWire.Core.Abstractions;

/// <summary>Abstracts text embedding generation with a large language model provider.</summary>
public interface ILlmEmbedClient
{
    /// <summary>Human-readable name of this provider, e.g. <c>openai</c>.</summary>
    string Name { get; }

    /// <summary>Generates a vector embedding for the given text.</summary>
    /// <param name="text">The input text to embed.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>A floating-point embedding vector.</returns>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
