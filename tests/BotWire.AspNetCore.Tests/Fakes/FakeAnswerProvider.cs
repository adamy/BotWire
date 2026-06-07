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

using System.Runtime.CompilerServices;
using BotWire.Core.Abstractions;
using BotWire.Core.Enums;
using BotWire.Core.Models;

namespace BotWire.AspNetCore.Tests.Fakes;

internal sealed class FakeAnswerProvider : IAnswerProvider
{
    public AnswerResult Result { get; set; } = new(AnswerStatus.Answered, "Test answer");

    public Task<AnswerResult> AnswerAsync(
        string message,
        ConversationSession session,
        ContactInfo? contact = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result);

    public async IAsyncEnumerable<BotEvent> StreamAsync(
        string message,
        ConversationSession session,
        ContactInfo? contact = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return BotEvent.Done(Result);
        await Task.CompletedTask;
    }
}
