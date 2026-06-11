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
using BotWire.Core.Enums;
using BotWire.Core.Models;
using Microsoft.Extensions.Logging;

namespace BotWire.Core.Conversation;

/// <summary>
/// Default <see cref="ISummaryCompressor"/>: summarises the oldest part of the send-history with
/// a single LLM call once the conversation grows past twice the configured interval.
/// </summary>
public sealed class SummaryCompressor : ISummaryCompressor
{
    /// <summary>
    /// Prefix marking the synthetic system message that holds the rolling conversation summary.
    /// Used to detect and fold a prior summary on the next compression.
    /// </summary>
    internal const string SummaryPrefix = "Previous conversation summary: ";

    private readonly ILlmChatClient _chat;
    private readonly ILogger<SummaryCompressor> _logger;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="chat">The LLM used to produce the one-sentence summary.</param>
    /// <param name="logger">Logger for fail-open diagnostics.</param>
    public SummaryCompressor(ILlmChatClient chat, ILogger<SummaryCompressor> logger)
    {
        _chat = chat;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<List<ChatMessage>> CompressAsync(
        IReadOnlyList<ChatMessage> sendHistory,
        int interval,
        CancellationToken cancellationToken = default)
    {
        if (interval <= 0)
            return [.. sendHistory];

        // A prior summary, if present, always sits at the front. Everything after it is verbatim turns.
        var hasPriorSummary = sendHistory.Count > 0 && IsSummary(sendHistory[0]);
        var turnStart = hasPriorSummary ? 1 : 0;
        var turnCount = sendHistory.Count - turnStart;

        // Block trigger: only compress once turns reach 2× the interval, so the LLM summary
        // call fires roughly once per `interval` messages rather than on every turn.
        if (turnCount < 2 * interval)
            return [.. sendHistory];

        var foldCount = turnCount - interval; // fold the oldest, keep the latest `interval`

        var toSummarize = new List<ChatMessage>(foldCount + 1);
        if (hasPriorSummary)
            toSummarize.Add(sendHistory[0]);
        for (var i = turnStart; i < turnStart + foldCount; i++)
            toSummarize.Add(sendHistory[i]);

        _logger.LogInformation(
            "BotWire: compressing send-history — folding {FoldCount} of {TurnCount} messages into a summary, keeping the latest {Keep}.",
            foldCount, turnCount, interval);

        var summaryText = await SummarizeAsync(toSummarize, cancellationToken);

        var result = new List<ChatMessage>(interval + 1)
        {
            new(ChatRole.System, SummaryPrefix + summaryText),
        };
        for (var i = turnStart + foldCount; i < sendHistory.Count; i++)
            result.Add(sendHistory[i]);

        _logger.LogDebug(
            "BotWire: send-history compressed to {Count} messages (1 summary + {Kept} recent).",
            result.Count, result.Count - 1);

        return result;
    }

    private async Task<string> SummarizeAsync(List<ChatMessage> messages, CancellationToken ct)
    {
        var prompt = new List<ChatMessage>(messages.Count + 1);
        prompt.AddRange(messages);
        prompt.Add(new(ChatRole.System,
            "Summarize the support conversation above in one sentence so it can be used as context " +
            "for the rest of the chat. Reply with only the summary sentence."));

        var raw = await _chat.ChatAsync(prompt, ct);
        return raw.Trim();
    }

    internal static bool IsSummary(ChatMessage message) =>
        message.Role == ChatRole.System &&
        message.Content.StartsWith(SummaryPrefix, StringComparison.Ordinal);
}
