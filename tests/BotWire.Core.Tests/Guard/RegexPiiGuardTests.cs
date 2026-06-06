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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BotWire.Core.Tests.Guard;

public class RegexPiiGuardTests
{
    private static RegexPiiGuard Create(Action<PiiGuardOptions>? configure = null)
    {
        var opts = new PiiGuardOptions();
        configure?.Invoke(opts);
        return new RegexPiiGuard(Options.Create(opts), NullLogger<RegexPiiGuard>.Instance);
    }

    // ----- IsEnabled -----

    [Fact]
    public void IsEnabled_ReturnsTrue_WhenEnabledOption()
    {
        Assert.True(Create().IsEnabled);
    }

    // ----- Default pattern blocks -----

    [Theory]
    [InlineData("contact me at user@example.com please", "email")]
    [InlineData("my email is Test.User+tag@Sub.Domain.co.uk", "email")]
    [InlineData("call me on 13812345678", "phone-cn")]
    [InlineData("ring +447911123456", "phone-intl")]
    [InlineData("card 4111 1111 1111 1111", "credit-card")]
    [InlineData("card 4111-1111-1111-1111", "credit-card")]
    public void Check_DefaultPatterns_Block(string message, string expectedPattern)
    {
        var result = Create().Check(message);
        Assert.True(result.Blocked);
        Assert.Equal(expectedPattern, result.MatchedPattern);
    }

    // ----- Clean messages pass -----

    [Theory]
    [InlineData("Hello, I need help with my order.")]
    [InlineData("My account number is 12345")]
    [InlineData("How do I reset my password?")]
    public void Check_CleanMessage_NotBlocked(string message)
    {
        var result = Create().Check(message);
        Assert.False(result.Blocked);
        Assert.Null(result.MatchedPattern);
    }

    // ----- AdditionalPatterns -----

    [Fact]
    public void Check_AdditionalPattern_Blocks()
    {
        var guard = Create(o => o.AdditionalPatterns.Add(@"\bSECRET\b"));
        var result = guard.Check("The value is SECRET");
        Assert.True(result.Blocked);
        Assert.Equal("custom-0", result.MatchedPattern);
    }

    [Fact]
    public void Check_InvalidAdditionalPattern_SkippedWithoutThrow()
    {
        var guard = Create(o => o.AdditionalPatterns.Add("[invalid"));
        var result = guard.Check("Hello");
        Assert.False(result.Blocked);
    }

    [Fact]
    public void Check_AdditionalPatternAfterInvalidOne_StillWorks()
    {
        var guard = Create(o =>
        {
            o.AdditionalPatterns.Add("[invalid");
            o.AdditionalPatterns.Add(@"\bTOKEN\b");
        });
        var result = guard.Check("my TOKEN here");
        Assert.True(result.Blocked);
        Assert.Equal("custom-1", result.MatchedPattern);
    }
}
