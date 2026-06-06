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

namespace BotWire.Core.Conversation;

/// <summary>Configuration options for <see cref="InMemoryConversationStore"/>.</summary>
public sealed class ConversationStoreOptions : IValidatableObject
{
    /// <summary>
    /// How long a session may remain idle before it is eligible for removal by the
    /// background cleanup sweep. Defaults to 2 hours.
    /// </summary>
    public TimeSpan SessionTtl { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// Maximum number of messages retained per session. When a save would exceed this,
    /// the oldest non-system messages are dropped while all system messages are kept.
    /// Defaults to 50.
    /// </summary>
    public int MaxHistoryMessages { get; set; } = 50;

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SessionTtl <= TimeSpan.Zero)
            yield return new ValidationResult(
                "SessionTtl must be greater than zero.",
                [nameof(SessionTtl)]);

        if (MaxHistoryMessages < 1)
            yield return new ValidationResult(
                "MaxHistoryMessages must be at least 1.",
                [nameof(MaxHistoryMessages)]);
    }
}
