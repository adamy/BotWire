# BotWire

**A 24/7 AI customer-support bot for your .NET app — self-hosted, bring-your-own-key, no SaaS fees.**

Drop one package into your ASP.NET Core site, point it at your FAQ, and ship a support assistant that answers customers instantly from *your* docs — and quietly opens a human ticket the moment one is actually needed. You supply an OpenAI-compatible API key, so your only running cost is model tokens (pennies per conversation on `gpt-4o-mini` or DeepSeek).

## Two lines to wire it up

```csharp
builder.Services.AddBotWire(opts =>
{
    opts.TopicDescription = "Online store customer support";
    opts.Documents        = ["docs/faq.md"];
    opts.ChatProvider     = new OpenAIProviderOptions { ApiKey = "sk-...", Model = "gpt-4o-mini" };
});

app.MapBotWire();
```

Embed the zero-dependency widget on any page:

```html
<script src="/botwire/widget.js"></script>
<botwire-widget data-endpoint="/support" data-title="Acme Support"></botwire-widget>
```

## What you get

- **Grounded answers.** Replies come only from the Markdown knowledge base you supply — no hallucinated policies, prices, or promises.
- **Knows when to get a human.** Collects contact details and raises a support ticket (emailed to your team) instead of guessing.
- **Streaming chat widget.** ~12 KB Web Component, Shadow DOM, no framework, one `<script>` tag.
- **Multilingual.** Replies in the customer's language; you pick the language your team reads tickets in.
- **Production guards.** PII + prompt-injection filters, five-dimension rate limiting (concurrency, per-minute, per-session, per-IP/hour, daily token budget), and an optional NDJSON audit log.
- **Bring your own model.** OpenAI, DeepSeek, Groq, or any OpenAI-compatible endpoint.

## Packages

| Package | Use it for |
|---|---|
| **BotWire.AspNetCore** | The full stack — start here. Endpoints, widget, guards, DI. |
| **BotWire.Core** | The engine alone (RAG, escalation, guards) with no ASP.NET dependency. |
| **BotWire.Channels.Email** | SMTP delivery of escalated tickets (MailKit). |

## License

AGPL-3.0-or-later. Commercial licenses available for proprietary use.

📖 **Full docs, configuration reference, and screenshots:** https://github.com/adamy/BotWire
