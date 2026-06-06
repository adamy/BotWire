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
    /// Maximum number of leading characters to inspect for a control word before failing open.
    /// Sized to cover the longest control word (<see cref="Escalate"/> = 8 chars) plus a newline,
    /// with headroom; the streaming path buffers until this limit when the model omits the
    /// expected newline.
    /// </summary>
    public const int ScanLimit = 20;

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
