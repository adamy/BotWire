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

using System.ComponentModel.DataAnnotations;
using RedactWire;

namespace BotWire.Core.Guard;

/// <summary>Configuration for PII detection. Detection is backed by
/// <see href="https://github.com/adamy/RedactWire">RedactWire</see>; use
/// <see cref="ConfigureDetector"/> to customize the underlying detector directly.</summary>
public sealed class PiiGuardOptions
{
    /// <summary>Enables PII detection. When false a <see cref="NullPiiGuard"/> is used and no rules are evaluated. Defaults to <see langword="true"/>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Message returned to the caller when PII is detected.</summary>
    [Required]
    public string RejectionMessage { get; set; } =
        "Your message contains sensitive information and cannot be processed.";

    /// <summary>
    /// Additional regex patterns (case-insensitive) to block, added as culture-agnostic
    /// custom rules after the built-in defaults. Each becomes a RedactWire
    /// <see cref="PiiType.Custom"/> rule; matches report as <c>invariant:custom-{index}</c>.
    /// Invalid regex strings are skipped with a warning log at startup.
    /// </summary>
    public IList<string> AdditionalPatterns { get; set; } = [];

    /// <summary>
    /// Optional hook to configure the RedactWire <see cref="PiiDetectorBuilder"/> directly —
    /// add cultures, custom <c>IPiiRule</c>s, or an overlap strategy. The builder starts from
    /// <see cref="PiiDetectorBuilder.CreateDefault"/> (invariant rules) with
    /// <see cref="PiiDetectorBuilder.AddSecretDetection"/> already applied and any
    /// <see cref="AdditionalPatterns"/> bridged in; when this is <see langword="null"/> the
    /// detector runs those defaults against the default culture
    /// (<see cref="System.Globalization.CultureInfo.CurrentCulture"/>).
    /// </summary>
    public Action<PiiDetectorBuilder>? ConfigureDetector { get; set; }
}
