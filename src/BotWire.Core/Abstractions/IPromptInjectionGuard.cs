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

namespace BotWire.Core.Abstractions;

/// <summary>Inspects user messages for prompt injection patterns and signals whether they should be blocked.</summary>
public interface IPromptInjectionGuard
{
    /// <summary>True when injection detection is active; false when the guard is a no-op.</summary>
    bool IsEnabled { get; }

    /// <summary>Returns <see langword="true"/> if <paramref name="message"/> appears to be a prompt injection attempt.</summary>
    /// <param name="message">The raw user message text to inspect.</param>
    bool IsInjectionAttempt(string message);
}
