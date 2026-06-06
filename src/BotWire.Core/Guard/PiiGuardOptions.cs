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

namespace BotWire.Core.Guard;

/// <summary>Configuration for PII detection.</summary>
public sealed class PiiGuardOptions
{
    /// <summary>Enables PII pattern matching. When false a <see cref="NullPiiGuard"/> is used and no patterns are evaluated. Defaults to <see langword="true"/>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Message returned to the caller when a PII pattern is matched.</summary>
    [Required]
    public string RejectionMessage { get; set; } =
        "Your message contains sensitive information and cannot be processed.";

    /// <summary>
    /// Additional regex patterns (case-insensitive) to block, appended after the built-in defaults.
    /// Invalid regex strings are skipped with a warning log at startup.
    /// </summary>
    public IList<string> AdditionalPatterns { get; set; } = [];

}
