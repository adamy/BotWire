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

using BotWire.Core.Guard;

namespace BotWire.Core.Tests.Guard;

public class NullPiiGuardTests
{
    [Fact]
    public void IsEnabled_ReturnsFalse()
    {
        Assert.False(NullPiiGuard.Instance.IsEnabled);
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("+447911123456")]
    [InlineData("Hello")]
    public void Check_NeverBlocks(string message)
    {
        var result = NullPiiGuard.Instance.Check(message);
        Assert.False(result.Blocked);
        Assert.Null(result.MatchedPattern);
    }
}
