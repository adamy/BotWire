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

namespace BotWire.Core.Rag;

/// <summary>Configuration for the RAG <see cref="AnswerProvider"/> (Phase 1, Mode A).</summary>
public sealed class AnswerProviderOptions
{
    /// <summary>
    /// Paths to the Markdown (<c>.md</c>) documents that form the bot's knowledge base. Their
    /// combined content is embedded verbatim in the system prompt (Mode A — no retrieval).
    /// </summary>
    public IList<string> DocumentPaths { get; set; } = [];

    /// <summary>
    /// Optional extra instructions prepended to the generated system prompt, e.g. a persona or
    /// tone guideline. The control-word protocol instruction is always appended by BotWire.
    /// </summary>
    public string? SystemPromptPreamble { get; set; }
}
