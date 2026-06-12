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

using System.Text;
using BotWire.Core.Rag;

namespace BotWire.Core.Tests.Rag;

public class JsonAnswerStreamReaderTests
{
    private sealed record Run(bool Resolved, bool OffTopic, string Action, string Message, bool Failed, string Raw);

    /// <summary>Feeds <paramref name="json"/> in fixed-size chunks to exercise cross-boundary handling.</summary>
    private static Run Feed(string json, int chunk, bool expectOffTopic = false)
    {
        var reader = new JsonAnswerStreamReader(expectOffTopic);
        var message = new StringBuilder();
        bool resolved = false, offtopic = false;
        var action = "";

        for (var i = 0; i < json.Length; i += chunk)
        {
            var part = json.Substring(i, Math.Min(chunk, json.Length - i));
            foreach (var o in reader.Feed(part))
            {
                if (o.Kind == JsonAnswerStreamReader.OutputKind.PreludeResolved)
                {
                    resolved = true;
                    offtopic = reader.OffTopic;
                    action = reader.Action;
                }
                else if (o.Kind == JsonAnswerStreamReader.OutputKind.MessageDelta)
                {
                    message.Append(o.Text);
                }
            }
        }
        reader.Finish();
        return new Run(resolved, offtopic, action, message.ToString(), reader.Failed, reader.Raw);
    }

    public static IEnumerable<object[]> ChunkSizes => [[1], [2], [3], [7], [1000]];

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void Answer_PlainMessage(int chunk)
    {
        var run = Feed("""{"offtopic":false,"action":"answer","message":"Hello world"}""", chunk);

        Assert.True(run.Resolved);
        Assert.False(run.OffTopic);
        Assert.Equal("answer", run.Action);
        Assert.Equal("Hello world", run.Message);
        Assert.False(run.Failed);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void Answer_WithJsonEscapes(int chunk)
    {
        // \n, escaped quote, backslash, and é (é)
        var run = Feed("""{"offtopic":false,"action":"answer","message":"line1\nsay \"hi\" \\ café"}""", chunk);

        Assert.Equal("line1\nsay \"hi\" \\ café", run.Message);
        Assert.False(run.Failed);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void Answer_WithRawNonAscii(int chunk)
    {
        var run = Feed("""{"offtopic":false,"action":"answer","message":"退款多久到账？"}""", chunk);
        Assert.Equal("退款多久到账？", run.Message);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void Answer_WithSurrogatePairEscape(int chunk)
    {
        // 🙂 = U+1F642 = 🙂 — surrogate halves may land in different chunks
        var run = Feed("""{"offtopic":false,"action":"answer","message":"hi 🙂"}""", chunk);
        Assert.Equal("hi 🙂", run.Message);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void Escalate_ResolvesAction(int chunk)
    {
        var run = Feed("""{"offtopic":false,"action":"escalate","message":"connecting you"}""", chunk);
        Assert.True(run.Resolved);
        Assert.Equal("escalate", run.Action);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void OffTopic_True(int chunk)
    {
        var run = Feed("""{"offtopic":true,"action":"answer","message":"x"}""", chunk);
        Assert.True(run.Resolved);
        Assert.True(run.OffTopic);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void Whitespace_BetweenTokens(int chunk)
    {
        var run = Feed(" { \"offtopic\" : true , \"action\" : \"escalate\" , \"message\" : \"hi\" } ", chunk);
        Assert.True(run.Resolved);
        Assert.True(run.OffTopic);
        Assert.Equal("escalate", run.Action);
        Assert.Equal("hi", run.Message);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void MissingOffTopic_DefaultsFalse(int chunk)
    {
        var run = Feed("""{"action":"answer","message":"hello"}""", chunk);
        Assert.True(run.Resolved);
        Assert.False(run.OffTopic);
        Assert.Equal("hello", run.Message);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void NonJson_Fails_AndPreservesRaw(int chunk)
    {
        const string prose = "Sorry, I can help with that. Here is the answer.";
        var run = Feed(prose, chunk);
        Assert.True(run.Failed);
        Assert.False(run.Resolved);
        Assert.Equal(prose, run.Raw);
    }

    [Fact]
    public void Raw_AlwaysAccumulatesFullResponse()
    {
        const string json = """{"offtopic":false,"action":"answer","message":"abc"}""";
        var run = Feed(json, 4);
        Assert.Equal(json, run.Raw);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void ExpectOffTopic_ResolvesOffTopicBeforeStreaming(int chunk)
    {
        var run = Feed("""{"offtopic":true,"action":"answer","message":"x"}""", chunk, expectOffTopic: true);
        Assert.True(run.Resolved);
        Assert.True(run.OffTopic);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void ReorderedMessageFirst_StillStreamsMessage(int chunk)
    {
        // Documented limitation: if the model emits message before action, the reader resolves at
        // the message value rather than waiting for action. The message still streams correctly;
        // the resolved action is chunk-dependent (defaults to "answer" when action has not yet
        // arrived, but is read when the whole prefix is already buffered), so it is not asserted.
        var run = Feed("""{"message":"hi there","action":"escalate"}""", chunk);
        Assert.True(run.Resolved);
        Assert.Equal("hi there", run.Message);
    }
}
