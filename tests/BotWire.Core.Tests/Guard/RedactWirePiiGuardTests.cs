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

using System.Globalization;
using BotWire.Core.Guard;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RedactWire;

namespace BotWire.Core.Tests.Guard;

public class RedactWirePiiGuardTests
{
    private static RedactWirePiiGuard Create(Action<PiiGuardOptions>? configure = null)
    {
        var opts = new PiiGuardOptions();
        configure?.Invoke(opts);
        return new RedactWirePiiGuard(Options.Create(opts), NullLogger<RedactWirePiiGuard>.Instance);
    }

    // ----- IsEnabled -----

    [Fact]
    public void IsEnabled_ReturnsTrue_WhenEnabledOption()
    {
        Assert.True(Create().IsEnabled);
    }

    [Fact]
    public void Check_ReturnsNotBlocked_WhenDisabled()
    {
        var result = Create(o => o.Enabled = false).Check("contact me at user@example.com");
        Assert.False(result.Blocked);
        Assert.Null(result.MatchedPattern);
    }

    // ----- Default invariant rules block (culture-agnostic) -----

    [Theory]
    [InlineData("contact me at user@example.com please", "invariant:Email")]
    [InlineData("card 4111 1111 1111 1111", "invariant:CreditCard")]
    [InlineData("server is 192.168.0.1 today", "invariant:IPv4")]
    public void Check_DefaultInvariantRules_Block(string message, string expectedRule)
    {
        var result = Create().Check(message);
        Assert.True(result.Blocked);
        Assert.Equal(expectedRule, result.MatchedPattern);
    }

    // ----- Secret detection on by default (AddSecretDetection) -----

    [Fact]
    public void Check_SecretToken_Blocked()
    {
        var result = Create().Check("my key is sk-proj-abcdefghijklmnopqrstuvwxyz0123");
        Assert.True(result.Blocked);
        Assert.Equal("invariant:OpenAiKey", result.MatchedPattern);
    }

    // ----- Clean messages pass -----

    [Theory]
    [InlineData("Hello, I need help with my order.")]
    [InlineData("How do I reset my password?")]
    public void Check_CleanMessage_NotBlocked(string message)
    {
        var result = Create().Check(message);
        Assert.False(result.Blocked);
        Assert.Null(result.MatchedPattern);
    }

    // ----- AdditionalPatterns (bridged to RedactWire custom rules) -----

    [Fact]
    public void Check_AdditionalPattern_Blocks()
    {
        var guard = Create(o => o.AdditionalPatterns.Add(@"\bSECRET\b"));
        var result = guard.Check("The value is SECRET");
        Assert.True(result.Blocked);
        Assert.Equal("invariant:custom-0", result.MatchedPattern);
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
        Assert.Equal("invariant:custom-1", result.MatchedPattern);
    }

    [Fact]
    public void Check_CatastrophicAdditionalPattern_TimesOutAndDoesNotBlock()
    {
        // Classic exponential-backtracking pattern + non-matching tail. The match timeout
        // must trip, degrade to "no match", and return quickly rather than hang.
        var guard = Create(o => o.AdditionalPatterns.Add(@"(a+)+$"));
        var input = new string('a', 40) + "!";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = guard.Check(input);
        sw.Stop();

        Assert.False(result.Blocked);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"took {sw.ElapsedMilliseconds}ms");
    }

    // ----- ConfigureDetector escape hatch -----

    [Fact]
    public void Check_CustomRuleViaConfigureDetector_Blocks()
    {
        var guard = Create(o => o.ConfigureDetector = b =>
            b.AddInvariantRule(new RegexRule("AcmeAccount", PiiType.Custom,
                @"(?<v>\bACME-\d{6}\b)", subtype: "AcmeAccount")));

        var result = guard.Check("ref ACME-123456 on file");
        Assert.True(result.Blocked);
        Assert.Equal("invariant:AcmeAccount", result.MatchedPattern);
    }

    [Fact]
    public void Check_CultureRuleViaConfigureDetector_Blocks()
    {
        var guard = Create(o => o.ConfigureDetector = b => b.AddCulture(new CultureInfo("en-US")));

        var result = guard.Check("my ssn is 123-45-6789");
        Assert.True(result.Blocked);
        Assert.StartsWith("en-US:", result.MatchedPattern);
    }
}
