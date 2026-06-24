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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedactWire;

namespace BotWire.Core.Guard;

/// <summary>
/// PII guard backed by <see href="https://github.com/adamy/RedactWire">RedactWire</see>.
/// Builds a single immutable <see cref="PiiDetector"/> at construction time from the default
/// invariant rules plus secret/credential detection, running against the default culture.
/// Consumers can customize the detector via <see cref="PiiGuardOptions.ConfigureDetector"/>.
/// </summary>
internal sealed class RedactWirePiiGuard : IPiiGuard
{
    // Cap on how long any one user-supplied pattern may scan a message, to bound ReDoS risk.
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(100);

    private readonly bool _enabled;
    private readonly PiiDetector _detector;

    public RedactWirePiiGuard(IOptions<PiiGuardOptions> options, ILogger<RedactWirePiiGuard> logger)
    {
        var opts = options.Value;
        _enabled = opts.Enabled;

        // CreateDefault() = invariant rules (email, credit card, IP, IBAN); AddSecretDetection()
        // = API keys/tokens. With no AddCulture, Build() resolves against CurrentCulture.
        var builder = PiiDetectorBuilder.CreateDefault().AddSecretDetection();

        // Bridge user regex patterns into culture-agnostic custom rules (case-insensitive,
        // matching the legacy guard). Compiled with a match timeout to bound ReDoS. Invalid
        // patterns are skipped with a warning, not thrown.
        for (var i = 0; i < opts.AdditionalPatterns.Count; i++)
        {
            try
            {
                builder.AddInvariantRule(
                    new TimeoutRegexRule($"custom-{i}", opts.AdditionalPatterns[i], PatternTimeout, logger));
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "BotWire: AdditionalPatterns[{Index}] is not a valid regex and was skipped.", i);
            }
        }

        opts.ConfigureDetector?.Invoke(builder);
        _detector = builder.Build();

        logger.LogInformation("BotWire: PiiGuard enabled (RedactWire), secret detection on.");
    }

    public bool IsEnabled => _enabled;

    public PiiCheckResult Check(string message)
    {
        // Blank input can hold no PII — skip detection (and its per-message allocation).
        if (!_enabled || string.IsNullOrWhiteSpace(message))
            return new PiiCheckResult(false, null);

        var result = _detector.Detect(message);
        if (!result.HasPii)
            return new PiiCheckResult(false, null);

        // Report the earliest match in the text (overlap resolution runs per group, so
        // AllMatches is not position-ordered). Rule id e.g. "invariant:Email", "en-US:SSN".
        var first = result.AllMatches.OrderBy(m => m.Start).First();
        return new PiiCheckResult(true, first.Rule);
    }
}
