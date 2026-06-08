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

using BotWire.Core.Rag;
using Microsoft.Extensions.Options;

namespace BotWire.Core.Tests.Rag;

public class DefaultSystemPromptBuilderTests
{
    private static DefaultSystemPromptBuilder Create(string? preamble = null) =>
        new(Options.Create(new AnswerProviderOptions { SystemPromptPreamble = preamble }));

    [Fact]
    public void Build_EmitsBothControlWords()
    {
        var prompt = Create().Build(["doc"]);
        Assert.Contains("ANSWER", prompt);
        Assert.Contains("ESCALATE", prompt);
    }

    [Fact]
    public void Build_IncludesDocumentContent()
    {
        var prompt = Create().Build(["password reset steps", "refund policy"]);
        Assert.Contains("password reset steps", prompt);
        Assert.Contains("refund policy", prompt);
    }

    [Fact]
    public void Build_NoDocuments_StatesNoneProvided()
    {
        var prompt = Create().Build([]);
        Assert.Contains("(no documents provided)", prompt);
    }

    [Fact]
    public void Build_IncludesPreambleAsScope()
    {
        var prompt = Create("Online store customer support").Build(["doc"]);
        Assert.Contains("Online store customer support", prompt);
    }

    [Fact]
    public void Build_NullPreamble_DoesNotThrowOrInjectScopeLine()
    {
        var prompt = Create(preamble: null).Build(["doc"]);
        Assert.DoesNotContain("Your support scope:", prompt);
    }

    [Fact]
    public void Build_ContainsMultilingualInstruction()
    {
        var prompt = Create().Build(["doc"]);
        Assert.Contains("same language", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_ContainsInjectionResistanceRules()
    {
        var prompt = Create().Build(["doc"]);
        // User input and documents must be treated as data, not instructions.
        Assert.Contains("as DATA", prompt);
        Assert.Contains("ignore previous instructions", prompt);
    }
}
