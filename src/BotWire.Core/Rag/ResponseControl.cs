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

using BotWire.Core.Enums;

namespace BotWire.Core.Rag;

/// <summary>
/// Parses the leading control word ("ANSWER" / "ESCALATE") that the system prompt instructs the
/// LLM to emit on the first line of every reply. Shared by the streaming and non-streaming paths.
/// </summary>
internal static class ResponseControl
{
    /// <summary>The control word signalling the model could answer from the documents.</summary>
    public const string Answer = "ANSWER";

    /// <summary>The control word signalling the model needs a human to take over.</summary>
    public const string Escalate = "ESCALATE";

    /// <summary>
    /// Maximum number of leading characters to buffer while waiting for a newline before failing
    /// open as ANSWER. Sized to comfortably exceed the longest control word plus any whitespace
    /// a model might emit before or after it on the first line, while staying short enough
    /// that a slow or non-compliant model does not hold up the response stream for long.
    /// </summary>
    public const int ScanLimit = 200;

    /// <summary>The result of parsing the control word from a full (non-streamed) response.</summary>
    /// <param name="Status">Resolved answer status.</param>
    /// <param name="Message">The response body with the control-word line stripped.</param>
    /// <param name="Recognized">False when no control word was found and the parser failed open.</param>
    public readonly record struct Result(AnswerStatus Status, string Message, bool Recognized);

    /// <summary>
    /// Parses <paramref name="raw"/>. If the first line is "ESCALATE" the status is
    /// <see cref="AnswerStatus.NeedHuman"/>; if "ANSWER" it is <see cref="AnswerStatus.Answered"/>.
    /// Any other leading content fails open to <see cref="AnswerStatus.Answered"/> with the full
    /// text preserved and <see cref="Result.Recognized"/> set to false.
    /// </summary>
    public static Result Parse(string raw)
    {
        var newline = raw.IndexOf('\n');
        var prefix = (newline >= 0 ? raw[..newline] : raw).Trim();

        if (StartsWith(prefix, Escalate))
            return new Result(AnswerStatus.NeedHuman, Body(raw, newline, Escalate), true);

        if (StartsWith(prefix, Answer))
            return new Result(AnswerStatus.Answered, Body(raw, newline, Answer), true);

        return new Result(AnswerStatus.Answered, raw, false);
    }

    /// <summary>Returns true if <paramref name="value"/> begins with the given control word, case-insensitively.</summary>
    public static bool StartsWith(string value, string word) =>
        value.StartsWith(word, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Extracts the response body: everything after the first newline, or — when the model put
    /// the body on the same line as the control word — everything after the word itself.
    /// </summary>
    internal static string Body(string raw, int newline, string word)
    {
        if (newline >= 0)
            return raw[(newline + 1)..].Trim();

        var afterWord = raw.Trim()[word.Length..];
        return afterWord.Trim();
    }
}
