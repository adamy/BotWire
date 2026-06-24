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

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedactWire;

namespace BotWire.Core.Guard;

/// <summary>
/// A custom RedactWire <see cref="IPiiRule"/> for a user-supplied regex pattern, compiled with a
/// match timeout so a catastrophic-backtracking pattern cannot hang message scanning (ReDoS).
/// On timeout the rule logs a warning and reports no match — a single slow pattern degrades to a
/// miss rather than stalling the request. Used to bridge <see cref="PiiGuardOptions.AdditionalPatterns"/>.
/// </summary>
internal sealed class TimeoutRegexRule : IPiiRule
{
    private readonly Regex _re;
    private readonly ILogger _logger;

    public string Name { get; }
    public PiiType Type => PiiType.Custom;
    public string? Subtype { get; }

    public TimeoutRegexRule(string name, string pattern, TimeSpan timeout, ILogger logger)
    {
        Name = name;
        Subtype = name;
        _logger = logger;
        // Throws ArgumentException for an invalid pattern; the caller catches and skips it.
        _re = new Regex(pattern,
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            timeout);
    }

    public IEnumerable<RuleHit> Find(string text)
    {
        var hits = new List<RuleHit>();
        try
        {
            foreach (Match m in _re.Matches(text))
            {
                var g = m.Groups["v"].Success ? m.Groups["v"] : (Group)m;
                hits.Add(new RuleHit(g.Value, g.Index, g.Length, 1.0, Subtype: Subtype));
            }
        }
        catch (RegexMatchTimeoutException ex)
        {
            _logger.LogWarning(ex,
                "BotWire: PII custom pattern '{Name}' timed out scanning a message; treated as no match.", Name);
            return Array.Empty<RuleHit>();
        }
        return hits;
    }
}
