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

using System.Text;

namespace BotWire.Core.Rag;

/// <summary>
/// Builds the system prompt that grounds the LLM in the supplied documents and enforces the
/// ANSWER / ESCALATE control-word protocol on the first line of every reply.
/// </summary>
internal static class SystemPromptBuilder
{
    private const string DocumentSeparator = "\n\n---\n\n";

    /// <summary>Composes the system prompt from an optional preamble and the loaded documents.</summary>
    /// <param name="documents">The knowledge-base document contents.</param>
    /// <param name="preamble">Optional custom instructions to prepend.</param>
    public static string Build(IReadOnlyList<string> documents, string? preamble)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(preamble))
            sb.Append(preamble.Trim()).Append("\n\n");

        sb.Append(
            "You are a customer-support assistant. Answer the user's question using ONLY the " +
            "reference documents below. Do not invent facts that are not supported by them.\n\n");

        sb.Append(
            $"The FIRST line of every reply MUST be exactly one of these words, on its own line:\n" +
            $"  {ResponseControl.Answer}   - you can answer the question from the reference documents\n" +
            $"  {ResponseControl.Escalate} - the documents do not contain the answer and a human is needed\n" +
            "Write your reply to the user on the following lines. Never repeat the word elsewhere. " +
            $"When you emit {ResponseControl.Escalate}, you may add a short reason on the lines that follow.\n\n");

        sb.Append("# Reference documents\n\n");
        if (documents.Count == 0)
            sb.Append("(no documents provided)");
        else
            sb.Append(string.Join(DocumentSeparator, documents));

        return sb.ToString();
    }
}
