# BotWire.Redis

**Redis-backed session store and distributed rate-limit counters for [BotWire](https://www.nuget.org/packages/BotWire.AspNetCore) — multi-container support in one line.**

By default BotWire keeps conversation sessions and rate-limit counters in process memory, which breaks behind a load balancer: a request routed to a different instance loses the session, and every limit is multiplied by the replica count. This package moves both into Redis so they are shared across all instances and survive restarts.

## One line to enable it

```csharp
builder.Services.AddBotWire(opts =>
{
    opts.TopicDescription = "Online store customer support";
    opts.Documents        = ["docs/faq.md"];
    opts.ChatProvider     = new OpenAIProviderOptions { ApiKey = "sk-...", Model = "gpt-4o-mini" };
})
.AddBotWireRedis("localhost:6379");   // registers a shared multiplexer + both Redis stores

app.MapBotWire();
```

Call it after `AddBotWire(...)`. Without it, BotWire behaves exactly as before (in-memory stores, per-process counters) — adding the package changes nothing until you call `AddBotWireRedis`.

## What you get

- **Shared conversation sessions.** Stored as JSON with a sliding TTL equal to `SessionTtl`; history survives an instance restart or load-balancer bounce.
- **Distributed rate limiting.** `MaxMessagesPerMinute`, `MaxSessionsPerIpPerHour`, and `DailyTokenBudget` use atomic Redis counters, so N instances enforce one shared limit instead of N×. The daily-budget key resets at UTC midnight.
- **No Core coupling.** Implemented behind `IRateLimitStore` / `IConversationStore`; `BotWire.Core` takes no Redis dependency.

`MaxConcurrentSessions` stays per-container by design (a distributed semaphore leaks permits on container crashes).

## Requirements

- A reachable Redis instance (any standard `StackExchange.Redis` connection string).
- `BotWire.AspNetCore` (or `BotWire.Core`) — this package extends an existing BotWire setup.

## License

AGPL-3.0-or-later. Commercial licenses available for proprietary use.

📖 **Full docs and a multi-instance sample:** https://github.com/adamy/BotWire
