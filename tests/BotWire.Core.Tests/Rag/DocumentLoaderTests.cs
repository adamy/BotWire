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

using BotWire.Core.Exceptions;
using BotWire.Core.Rag;
using Microsoft.Extensions.Logging.Abstractions;

namespace BotWire.Core.Tests.Rag;

public class DocumentLoaderTests
{
    private static DocumentLoader CreateLoader() =>
        new(NullLogger<DocumentLoader>.Instance);

    private static string WriteTempMarkdown(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"botwire-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task LoadAsync_ValidMarkdown_ReturnsContent()
    {
        var path = WriteTempMarkdown("# Title\nhello");
        try
        {
            var loader = CreateLoader();

            var result = await loader.LoadAsync([path]);

            Assert.Single(result);
            Assert.Equal("# Title\nhello", result[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_OverTokenBudget_Throws()
    {
        // ~8001 tokens at 4 chars/token => 32004 chars, just over the 8000-token limit.
        var path = WriteTempMarkdown(new string('x', 32004));
        try
        {
            var loader = CreateLoader();

            await Assert.ThrowsAsync<BotWireConfigurationException>(() => loader.LoadAsync([path]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_NonMarkdownPath_Throws()
    {
        var loader = CreateLoader();

        await Assert.ThrowsAsync<BotWireConfigurationException>(
            () => loader.LoadAsync(["notes.txt"]));
    }

    [Fact]
    public async Task LoadAsync_MissingFile_Throws()
    {
        var loader = CreateLoader();
        var missing = Path.Combine(Path.GetTempPath(), $"botwire-missing-{Guid.NewGuid():N}.md");

        await Assert.ThrowsAsync<BotWireConfigurationException>(
            () => loader.LoadAsync([missing]));
    }
}
