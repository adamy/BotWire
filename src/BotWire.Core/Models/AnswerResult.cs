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
/// <param name="Status">Whether the bot answered or needs to escalate.</param>
/// <param name="Message">The bot's response text.</param>
/// <param name="FailedOpen">
/// True when no ANSWER / ESCALATE control word was found and the provider fell back to treating
/// the response as an ANSWER. The next turn should inject a stricter reminder.
/// </param>
public sealed record AnswerResult(AnswerStatus Status, string Message, bool FailedOpen = false);
