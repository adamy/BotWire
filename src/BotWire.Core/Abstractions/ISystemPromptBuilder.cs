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

/// <summary>
/// Builds the system prompt that grounds the LLM in the supplied knowledge-base documents.
/// Register a custom implementation via DI (before or after <c>AddBotWire</c>) to fully replace
/// the built-in prompt.
/// </summary>
/// <remarks>
/// <para>
/// A custom builder takes over the entire prompt, so it MUST preserve the control-word contract
/// the rest of BotWire depends on: instruct the model to emit, as the FIRST line of every reply
/// and alone on that line, exactly one of the English control words <c>ANSWER</c> (the bot can
/// answer from the documents) or <c>ESCALATE</c> (a human is needed). The user-facing message
/// follows on subsequent lines. If this contract is broken, escalation and ticket creation stop
/// working.
/// </para>
/// </remarks>
public interface ISystemPromptBuilder
{
    /// <summary>Composes the system prompt for the given knowledge-base document contents.</summary>
    /// <param name="documents">The loaded knowledge-base document contents, in configured order.</param>
    /// <returns>The complete system prompt string.</returns>
    string Build(IReadOnlyList<string> documents);
}
