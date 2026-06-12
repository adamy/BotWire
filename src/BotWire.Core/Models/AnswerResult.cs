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

using BotWire.Core.Enums;

namespace BotWire.Core.Models;

/// <summary>The outcome of a bot answer attempt.</summary>
/// <param name="Status">Whether the bot answered, needs to escalate, or was off-topic.</param>
/// <param name="Message">The bot's response text.</param>
/// <param name="FailedOpen">Reserved; currently always false (the provider retries instead of failing open).</param>
/// <param name="RawResponse">The full, unparsed LLM response (the answer JSON), for audit logging.</param>
/// <param name="TokensUsed">Total tokens billed for the LLM call(s) behind this answer; <c>0</c> when unreported.</param>
public sealed record AnswerResult(
    AnswerStatus Status, string Message, bool FailedOpen = false, string? RawResponse = null, int TokensUsed = 0);
