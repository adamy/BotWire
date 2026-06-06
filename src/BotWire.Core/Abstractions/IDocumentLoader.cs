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

/// <summary>Loads document content from file paths for use as RAG context.</summary>
public interface IDocumentLoader
{
    /// <summary>Loads and returns the text content of the given file paths.</summary>
    /// <param name="paths">Paths to the documents to load.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Ordered list of document contents corresponding to <paramref name="paths"/>.</returns>
    Task<IReadOnlyList<string>> LoadAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default);
}
