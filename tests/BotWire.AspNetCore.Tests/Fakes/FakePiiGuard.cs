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

using BotWire.Core.Abstractions;

namespace BotWire.AspNetCore.Tests.Fakes;

internal sealed class FakePiiGuard : IPiiGuard
{
    public static FakePiiGuard Allow => new() { Blocks = false };
    public static FakePiiGuard Block => new() { Blocks = true };

    public bool Blocks { get; init; }
    public bool IsEnabled => true;

    public PiiCheckResult Check(string message)
        => Blocks
            ? new PiiCheckResult(true,  "test-pattern")
            : new PiiCheckResult(false, null);
}
