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
/// Incrementally parses the streamed answer JSON
/// <c>{ "offtopic": false, "action": "answer"|"escalate", "message": "..." }</c> so the
/// <c>message</c> value can be streamed to the user token by token.
/// <para>
/// Resolution rule: the reader waits until <c>action</c> (and <c>offtopic</c>, when expected) have
/// fully arrived before it emits <see cref="OutputKind.PreludeResolved"/> and starts streaming the
/// message. These fields precede <c>message</c> in the schema, so in the normal case they resolve
/// first. If the <c>message</c> value is reached before they do (a model that reordered the fields),
/// the reader resolves with whatever it has — defaulting <c>action</c> to <c>answer</c> — and simply
/// streams the message. JSON escapes (including <c>\uXXXX</c> and surrogate pairs) are decoded across
/// chunk boundaries.
/// </para>
/// <para>
/// If the stream is not the expected JSON object, <see cref="Failed"/> is set and the caller should
/// fall back (retry, or treat <see cref="Raw"/> as a plain answer).
/// </para>
/// </summary>
internal sealed class JsonAnswerStreamReader
{
    /// <summary>Cap on the prefix buffered while waiting for the fields before giving up.</summary>
    private const int PreludeScanLimit = 4000;

    internal enum OutputKind { PreludeResolved, MessageDelta }

    internal readonly record struct Output(OutputKind Kind, string? Text);

    private enum Phase { Prelude, Message, AfterMessage, Failed }

    private enum Escape { None, Backslash, Unicode }

    private readonly bool _expectOffTopic;
    private readonly StringBuilder _raw = new();
    private readonly StringBuilder _prelude = new();

    private Phase _phase = Phase.Prelude;
    private bool _decided;
    private Escape _escape = Escape.None;
    private int _unicodeValue;
    private int _unicodeDigits;

    /// <param name="expectOffTopic">
    /// True when the topic guard is enabled and the schema therefore carries an <c>offtopic</c>
    /// field that should be resolved before streaming begins.
    /// </param>
    public JsonAnswerStreamReader(bool expectOffTopic = false)
    {
        _expectOffTopic = expectOffTopic;
    }

    /// <summary>The full accumulated raw response, for the fallback path and audit logging.</summary>
    public string Raw => _raw.ToString();

    /// <summary>True once <c>offtopic</c>/<c>action</c> have been resolved (or defaulted) from the prefix.</summary>
    public bool PreludeResolved { get; private set; }

    /// <summary>Value of the <c>offtopic</c> field (false when absent).</summary>
    public bool OffTopic { get; private set; }

    /// <summary>Value of the <c>action</c> field: <c>answer</c> or <c>escalate</c> (defaults to answer).</summary>
    public string Action { get; private set; } = "answer";

    /// <summary>True when the stream is not the expected JSON; caller should fall back to <see cref="Raw"/>.</summary>
    public bool Failed { get; private set; }

    public IReadOnlyList<Output> Feed(string delta)
    {
        _raw.Append(delta);
        var outputs = new List<Output>();
        if (_phase is Phase.Failed or Phase.AfterMessage)
            return outputs;

        if (_phase == Phase.Prelude)
        {
            _prelude.Append(delta);
            if (!TryAdvancePrelude(delta.Length, outputs, out var messageStartInDelta))
                return outputs;

            // Transitioned into the message value; decode the tail of this delta.
            var decoded = DecodeMessage(delta, messageStartInDelta);
            if (decoded.Length > 0)
                outputs.Add(new Output(OutputKind.MessageDelta, decoded));
            return outputs;
        }

        var more = DecodeMessage(delta, 0);
        if (more.Length > 0)
            outputs.Add(new Output(OutputKind.MessageDelta, more));
        return outputs;
    }

    /// <summary>Called after the stream ends; marks failure if the prelude never resolved.</summary>
    public void Finish()
    {
        if (_phase == Phase.Prelude && !PreludeResolved)
            Failed = true;
    }

    // ── Prelude ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Decides <c>offtopic</c>/<c>action</c> once they (or the message value) are available, then
    /// reports where the message value begins inside the current delta. Returns false while still
    /// buffering the prefix (or on failure).
    /// </summary>
    private bool TryAdvancePrelude(int currentDeltaLength, List<Output> outputs, out int messageValueStartInDelta)
    {
        messageValueStartInDelta = 0;

        var prefix = _prelude.ToString();
        var trimmed = prefix.AsSpan().TrimStart();
        if (trimmed.Length > 0 && trimmed[0] != '{')
        {
            Failed = true; // not a JSON object — fall back to raw
            _phase = Phase.Failed;
            return false;
        }

        var valueStart = FindMessageValueStart(prefix);

        if (!_decided)
        {
            var actionReady = ParseString(prefix, "action") is not null;          // non-null ⇒ value complete
            var offtopicReady = !_expectOffTopic || BoolComplete(prefix, "offtopic");

            if ((actionReady && offtopicReady) || valueStart >= 0)
            {
                Decide(prefix, outputs);
            }
            else
            {
                if (_prelude.Length > PreludeScanLimit) { Failed = true; _phase = Phase.Failed; }
                return false;
            }
        }

        if (valueStart < 0)
        {
            if (_prelude.Length > PreludeScanLimit) { Failed = true; _phase = Phase.Failed; }
            return false;
        }

        _phase = Phase.Message;
        var deltaStart = _prelude.Length - currentDeltaLength;
        messageValueStartInDelta = Math.Max(0, valueStart - deltaStart);
        return true;
    }

    private void Decide(string prefix, List<Output> outputs)
    {
        OffTopic = ParseBool(prefix, "offtopic");
        Action = ParseString(prefix, "action") is { Length: > 0 } a ? a.Trim().ToLowerInvariant() : "answer";
        PreludeResolved = true;
        _decided = true;
        outputs.Add(new Output(OutputKind.PreludeResolved, null));
    }

    /// <summary>Returns the index in <paramref name="s"/> of the first character of the message string value, or -1.</summary>
    private static int FindMessageValueStart(string s)
    {
        var key = s.IndexOf("\"message\"", StringComparison.Ordinal);
        if (key < 0) return -1;

        var i = key + "\"message\"".Length;
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        if (i >= s.Length || s[i] != ':') return -1;
        i++;
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        if (i >= s.Length || s[i] != '"') return -1;
        return i + 1; // first character inside the value
    }

    /// <summary>True when a complete <c>true</c>/<c>false</c> literal is present for the key.</summary>
    private static bool BoolComplete(string s, string key)
    {
        var idx = FindValueStart(s, key);
        if (idx < 0) return false;
        var span = s.AsSpan(idx);
        return span.StartsWith("true", StringComparison.OrdinalIgnoreCase)
            || span.StartsWith("false", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ParseBool(string s, string key)
    {
        var idx = FindValueStart(s, key);
        return idx >= 0 && s.AsSpan(idx).StartsWith("true", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ParseString(string s, string key)
    {
        var idx = FindValueStart(s, key);
        if (idx < 0 || idx >= s.Length || s[idx] != '"') return null;
        var end = s.IndexOf('"', idx + 1);
        return end < 0 ? null : s[(idx + 1)..end];
    }

    /// <summary>Index of the first value character after <c>"key" :</c> (whitespace skipped), or -1.</summary>
    private static int FindValueStart(string s, string key)
    {
        var token = "\"" + key + "\"";
        var k = s.IndexOf(token, StringComparison.Ordinal);
        if (k < 0) return -1;
        var i = k + token.Length;
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        if (i >= s.Length || s[i] != ':') return -1;
        i++;
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        return i;
    }

    // ── Message value decoding ─────────────────────────────────────────────────────

    private string DecodeMessage(string delta, int start)
    {
        var sb = new StringBuilder();
        for (var i = start; i < delta.Length; i++)
        {
            var c = delta[i];

            switch (_escape)
            {
                case Escape.Unicode:
                    _unicodeValue = (_unicodeValue << 4) + HexValue(c);
                    if (++_unicodeDigits == 4)
                    {
                        sb.Append((char)_unicodeValue); // surrogate pairs form naturally across two \u
                        _escape = Escape.None;
                    }
                    continue;

                case Escape.Backslash:
                    switch (c)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u': _escape = Escape.Unicode; _unicodeValue = 0; _unicodeDigits = 0; continue;
                        default: sb.Append(c); break; // lenient: pass through unknown escapes
                    }
                    _escape = Escape.None;
                    continue;

                default:
                    if (c == '\\') _escape = Escape.Backslash;
                    else if (c == '"') { _phase = Phase.AfterMessage; return sb.ToString(); }
                    else sb.Append(c);
                    continue;
            }
        }

        return sb.ToString();
    }

    private static int HexValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => 0,
    };
}
