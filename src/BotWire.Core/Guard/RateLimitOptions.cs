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

using System.ComponentModel.DataAnnotations;

namespace BotWire.Core.Guard;

/// <summary>
/// The five rate-limiting dimensions (design 008). Each is independently configurable; set a
/// dimension to <c>0</c> to disable it. Counters are in-memory and therefore per-process —
/// multi-instance deployments need a shared store (Redis), planned for Phase 3.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>
    /// Maximum number of concurrent in-flight answer requests across all sessions. When the cap is
    /// reached, additional requests QUEUE and wait for a slot rather than being rejected.
    /// Defaults to <c>100</c>. <c>0</c> disables the limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int MaxConcurrentSessions { get; set; } = 100;

    /// <summary>
    /// Maximum messages per session per rolling minute. Over the cap the request is DELAYED
    /// (not rejected) until a slot frees up, so the user perceives slowness, not an error.
    /// Defaults to <c>5</c>. <c>0</c> disables the limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int MaxMessagesPerMinute { get; set; } = 5;

    /// <summary>
    /// Maximum total messages in a single session. Over the cap the user is prompted to start a
    /// new conversation (see <see cref="SessionMessageCapMessage"/>).
    /// Defaults to <c>50</c>. <c>0</c> disables the limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int MaxMessagesPerSession { get; set; } = 50;

    /// <summary>
    /// Maximum new sessions a single IP may create per rolling hour. Over the cap, session
    /// creation is REJECTED (see <see cref="IpSessionCapMessage"/>).
    /// Defaults to <c>10</c>. <c>0</c> disables the limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int MaxSessionsPerIpPerHour { get; set; } = 10;

    /// <summary>
    /// Global daily token budget across all LLM calls, summed from the provider's reported usage
    /// and reset at midnight UTC. Over budget, requests receive a degraded response
    /// (see <see cref="TokenBudgetMessage"/>) instead of calling the model.
    /// Defaults to <c>500_000</c>. <c>0</c> disables the limit.
    /// <para>The running total is in-memory and per-process: it resets on restart and is not shared
    /// across instances. Durable, cross-instance budgeting (Redis) is planned for Phase 3.</para>
    /// </summary>
    [Range(0, long.MaxValue)]
    public long DailyTokenBudget { get; set; } = 500_000;

    /// <summary>Shown when <see cref="MaxMessagesPerSession"/> is exceeded.</summary>
    public string SessionMessageCapMessage { get; set; } =
        "This conversation has reached its message limit. Please start a new chat to continue.";

    /// <summary>Returned when <see cref="MaxSessionsPerIpPerHour"/> is exceeded.</summary>
    public string IpSessionCapMessage { get; set; } =
        "Too many new conversations from your network. Please try again later.";

    /// <summary>Returned when the <see cref="DailyTokenBudget"/> is exhausted.</summary>
    public string TokenBudgetMessage { get; set; } =
        "Our assistant is unusually busy right now. Please email us and we'll get back to you as soon as possible.";
}
