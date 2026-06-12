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

namespace BotWire.Core.Models;

/// <summary>
/// The result of a non-streaming chat completion: the reply text plus the total token
/// count the provider reported for the call (prompt + completion). <see cref="TotalTokens"/>
/// is <c>0</c> when the provider did not report usage.
/// </summary>
/// <param name="Text">The LLM's full response text.</param>
/// <param name="TotalTokens">Total tokens billed for the call, or <c>0</c> when unknown.</param>
public sealed record LlmChatResult(string Text, int TotalTokens = 0);
