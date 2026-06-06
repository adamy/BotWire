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

using BotWire.Core.Abstractions;
using BotWire.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace BotWire.Core.Rag;

/// <summary>
/// Loads Markdown documents from disk for use as RAG context (Phase 1, Mode A). Enforces a token
/// budget at load time: in Mode A the entire knowledge base is stuffed into the system prompt, so
/// an oversized corpus is a configuration error rather than something to silently truncate.
/// </summary>
public sealed class DocumentLoader : IDocumentLoader
{
    /// <summary>Rough characters-per-token ratio used to estimate prompt size without a tokenizer.</summary>
    private const int CharsPerToken = 4;

    /// <summary>Maximum estimated tokens permitted for the combined document set in Mode A.</summary>
    private const int TokenBudget = 8000;

    private readonly ILogger<DocumentLoader> _logger;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="logger">Logger for the load summary and diagnostics.</param>
    public DocumentLoader(ILogger<DocumentLoader> logger) => _logger = logger;

    /// <inheritdoc/>
    /// <exception cref="BotWireConfigurationException">
    /// A path is not a <c>.md</c> file, a file is missing, or the combined estimated token count
    /// exceeds the Phase 1 budget of 8000 tokens.
    /// </exception>
    public async Task<IReadOnlyList<string>> LoadAsync(
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken = default)
    {
        var contents = new List<string>(paths.Count);
        var totalChars = 0;

        foreach (var path in paths)
        {
            if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                throw new BotWireConfigurationException(
                    $"BotWire Phase 1 only supports Markdown (.md) documents; got '{path}'.");

            string content;
            try
            {
                content = await File.ReadAllTextAsync(path, cancellationToken);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                throw new BotWireConfigurationException(
                    $"BotWire could not find the document '{path}'.", ex);
            }

            contents.Add(content);
            totalChars += content.Length;

            var runningTokens = totalChars / CharsPerToken;
            if (runningTokens > TokenBudget)
                throw new BotWireConfigurationException(
                    $"BotWire: documents total ~{runningTokens} tokens, exceeding the Phase 1 limit " +
                    $"of {TokenBudget}. Reduce the document set; retrieval (Mode B) arrives in a later phase.");
        }

        _logger.LogInformation(
            "BotWire: loaded {DocumentCount} documents, ~{TokenEstimate} tokens",
            contents.Count, totalChars / CharsPerToken);

        return contents;
    }
}
