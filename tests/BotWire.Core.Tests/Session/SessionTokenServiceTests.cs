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

using BotWire.Core.Session;

namespace BotWire.Core.Tests.Session;

public class SessionTokenServiceTests
{
    private static readonly SessionTokenService _svc = new();

    [Fact]
    public void GenerateToken_Returns43Chars()
    {
        var token = _svc.GenerateToken();
        Assert.Equal(43, token.Length);
    }

    [Fact]
    public void GenerateToken_IsBase64UrlNoPadding()
    {
        var token = _svc.GenerateToken();
        Assert.Matches(@"^[A-Za-z0-9\-_]+$", token);
        Assert.DoesNotContain("=", token);
        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
    }

    [Fact]
    public void GenerateToken_1000CallsAreUnique()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => _svc.GenerateToken()).ToHashSet();
        Assert.Equal(1000, tokens.Count);
    }
}
