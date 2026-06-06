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
using BotWire.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotWire.Core.Guard;

/// <summary>
/// Regex-based PII guard. Compiles default patterns (email, phone-cn, phone-intl, credit-card)
/// plus any user-supplied patterns at construction time and checks every message against them.
/// </summary>
internal sealed class RegexPiiGuard : IPiiGuard
{
    private static readonly (string Pattern, string Name)[] DefaultPatterns =
    [
        (@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", "email"),
        (@"1[3-9]\d{9}", "phone-cn"),
        (@"\+\d{7,15}", "phone-intl"),
        (@"\b(?:\d[ -]?){13,16}\b", "credit-card"),
    ];

    private readonly bool _enabled;
    private readonly int _maxMessageLength;
    private readonly (Regex Regex, string Name)[] _compiled;

    public RegexPiiGuard(IOptions<PiiGuardOptions> options, ILogger<RegexPiiGuard> logger)
    {
        var opts = options.Value;
        _enabled = opts.Enabled;
        _maxMessageLength = opts.MaxMessageLength;

        var list = new List<(Regex, string)>(DefaultPatterns.Length + opts.AdditionalPatterns.Count);

        foreach (var (pattern, name) in DefaultPatterns)
            list.Add((new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), name));

        for (var i = 0; i < opts.AdditionalPatterns.Count; i++)
        {
            try
            {
                list.Add((
                    new Regex(opts.AdditionalPatterns[i], RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                    $"custom-{i}"));
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "BotWire: AdditionalPatterns[{Index}] is not a valid regex and was skipped.", i);
            }
        }

        _compiled = [.. list];

        logger.LogInformation("BotWire: PiiGuard enabled, patterns: {Patterns}",
            string.Join(", ", _compiled.Select(p => p.Name)));
    }

    public bool IsEnabled => _enabled;

    public PiiCheckResult Check(string message)
    {
        if (!_enabled)
            return new PiiCheckResult(false, null);

        if (message.Length > _maxMessageLength)
            return new PiiCheckResult(true, "max-length");

        foreach (var (regex, name) in _compiled)
        {
            if (regex.IsMatch(message))
                return new PiiCheckResult(true, name);
        }
        return new PiiCheckResult(false, null);
    }
}
