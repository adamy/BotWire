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
/// Heuristic regex-based <see cref="IPromptInjectionGuard"/>. Stops casual and scripted prompt
/// injection attempts with zero added latency. Not designed to defeat a determined adversary —
/// input quoting in the system prompt is the primary mitigation; this is a secondary layer.
/// </summary>
public sealed class PatternPromptInjectionGuard : IPromptInjectionGuard
{
    private static readonly (string Pattern, string Name)[] DefaultPatterns =
    [
        (@"ignore\s+(all\s+|the\s+)?(previous|prior|above)\s+instructions?",  "ignore-instructions"),
        (@"disregard\s+(all\s+|the\s+)?(previous|prior|above)",               "disregard"),
        (@"\byou\s+are\s+now\b",                                       "you-are-now"),
        (@"\bnew\s+persona\b",                                         "new-persona"),
        (@"\bDAN\b",                                                   "dan"),
        (@"\bjailbreak\b",                                             "jailbreak"),
        (@"(?:^|\n)\s*system\s*:",                                     "system-turn"),
        (@"(?:^|\n)\s*assistant\s*:",                                 "assistant-turn"),
        (@"\boverride\s+(your\s+)?(instructions|rules|guidelines)",    "override"),
        (@"\bforget\s+(everything|all|your\s+instructions)",            "forget"),

        // Chinese (Simplified / Traditional)
        (@"忽略.{0,10}(指令|指示|规则|規則)",                    "zh-ignore-instructions"),
        (@"(忘记|忘記).{0,10}(指令|指示|规则|規則)",             "zh-forget-instructions"),
        (@"你(现在|現在)是",                                      "zh-you-are-now"),

        // Spanish
        (@"\bignorar?\s+(todas?\s+)?(las\s+)?instrucciones\b",    "es-ignore-instructions"),
        (@"\bolvidar?\s+(todas?\s+)?(las\s+)?instrucciones\b",    "es-forget-instructions"),
        (@"\bahora\s+eres\b",                                       "es-you-are-now"),

        // French
        (@"\bignore[rz]?\s+(toutes?\s+)?(les\s+)?instructions\b",  "fr-ignore-instructions"),
        (@"\boublie[rz]?\s+(toutes?\s+)?(les\s+)?instructions\b",  "fr-forget-instructions"),
        (@"\btu\s+es\s+maintenant\b",                              "fr-you-are-now"),

        // German
        (@"\bignoriere?\s+(alle\s+)?(vorherigen\s+)?anweisungen\b",  "de-ignore-instructions"),
        (@"\bvergiss\s+(alle\s+)?anweisungen\b",                     "de-forget-instructions"),
        (@"\bdu\s+bist\s+jetzt\b",                                   "de-you-are-now"),

        // Japanese
        (@"(指示|命令|ルール)を無視",  "ja-ignore-instructions"),
        (@"(指示|命令|ルール)を忘れ",  "ja-forget-instructions"),
        (@"あなたは今",               "ja-you-are-now"),

        // Korean
        (@"(지시|명령|규칙).{0,5}무시",  "ko-ignore-instructions"),
        (@"(지시|명령|규칙).{0,5}잊",    "ko-forget-instructions"),
        (@"당신은\s*이제",               "ko-you-are-now"),

        // Portuguese
        (@"\bignorar?\s+(todas?\s+)?(as\s+)?instruções\b",       "pt-ignore-instructions"),
        (@"\besqueç[ae]\s+(todas?\s+)?(as\s+)?instruções\b",    "pt-forget-instructions"),
        (@"\bagora\s+(você|voce|tu)\s+[eé]s?\b",                "pt-you-are-now"),
    ];

    private readonly (Regex Regex, string Name)[] _compiled;

    /// <summary>Initializes the guard, compiling all default and additional patterns.</summary>
    public PatternPromptInjectionGuard(
        IOptions<PromptInjectionOptions> options,
        ILogger<PatternPromptInjectionGuard> logger)
    {
        var opts = options.Value;
        var list = new List<(Regex, string)>(DefaultPatterns.Length + opts.AdditionalPatterns.Count);

        foreach (var (pattern, name) in DefaultPatterns)
            list.Add((
                new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                name));

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
                logger.LogWarning(ex,
                    "BotWire: PromptInjectionOptions.AdditionalPatterns[{Index}] is not a valid regex and was skipped.", i);
            }
        }

        _compiled = [.. list];
        logger.LogInformation("BotWire: PromptInjectionGuard enabled, patterns: {Patterns}",
            string.Join(", ", _compiled.Select(p => p.Name)));
    }

    /// <inheritdoc/>
    public bool IsEnabled => true;

    /// <inheritdoc/>
    public bool IsInjectionAttempt(string message)
    {
        foreach (var (regex, _) in _compiled)
        {
            if (regex.IsMatch(message))
                return true;
        }
        return false;
    }
}
