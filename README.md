# BotWire

**Low-cost AI customer-support for your .NET website.** Drop one NuGet package into your ASP.NET Core app, point it at your FAQ, and ship a 24/7 support assistant that answers customers instantly — and quietly opens a support ticket the moment a human is actually needed.

No SaaS seat fees. No per-conversation pricing. You bring your own OpenAI-compatible API key, so your only running cost is the model tokens — pennies per conversation with `gpt-4o-mini` or DeepSeek.

![BotWire chat widget on a demo store](docs/images/01-landing.png)

## Why BotWire

- **Cheap to run.** No platform subscription. Bring your own key; pay only for model tokens. Run it on `gpt-4o-mini`, DeepSeek, or any OpenAI-compatible endpoint to keep costs near zero.
- **One package, two lines of code.** `AddBotWire()` + `MapBotWire()`. The chat widget, streaming endpoint, escalation logic, and ticket email are all included.
- **Grounded in *your* docs.** Answers come only from the Markdown knowledge base you supply — no hallucinated policies, prices, or promises.
- **Knows when to get a human.** When a customer needs account/order access or asks for a person, BotWire collects their contact details and raises a support ticket instead of guessing.
- **Multilingual out of the box.** Replies in whatever language the customer writes in; you choose the language your team reads tickets in.
- **Zero-dependency widget.** A ~12KB Web Component (Shadow DOM, no framework) you embed with a single `<script>` tag.
- **Self-hostable & open.** AGPL-3.0. Your data and prompts stay in your app. Commercial licenses available if AGPL doesn't fit.

## How it works

**1. The customer asks — BotWire answers from your FAQ, streaming token-by-token.**

![Bot answering a return-policy question](docs/images/02-answer.png)

**2. When the question needs a human, it collects contact details instead of guessing.**

![Bot collecting customer email for escalation](docs/images/03-collect-contact.png)

**3. A support ticket is created and emailed to your team — the customer gets a confirmation.**

![Support ticket confirmation in the widget](docs/images/04-ticket.png)

## Quick start

Install the package:

```powershell
dotnet add package BotWire.AspNetCore
```

Wire it up in `Program.cs`:

```csharp
builder.Services.AddBotWire(opts =>
{
    opts.TopicDescription = "Online store customer support";
    opts.Documents        = ["docs/faq.md"];
    opts.ChatProvider     = new OpenAIProviderOptions { ApiKey = "sk-...", Model = "gpt-4o-mini" };

    // Optional: email tickets to your team when the bot escalates.
    opts.Email = new EmailOptions
    {
        SmtpHost = "smtp.example.com", Port = 587,
        FromAddress = "support-bot@example.com", ToAddress = "support@example.com",
    };
});

app.UseCors();
app.MapBotWire();
```

Embed the widget on any page:

```html
<script src="/botwire/widget.js"></script>
<botwire-widget
    data-endpoint="/support"
    data-title="Acme Support"
    data-primary-color="#6366f1"
    data-position="bottom-right">
</botwire-widget>
```

That's it — the bot answers from `docs/faq.md` and raises tickets when it can't.

## Configuration reference

| Property | Default | Description |
|---|---|---|
| `TopicDescription` | *(required)* | Short phrase describing your support scope, injected into the system prompt. |
| `Documents` | `[]` | Paths to Markdown knowledge-base files. |
| `ChatProvider` | *(required)* | LLM provider — `ApiKey`, `Model`, optional `BaseUrl` for OpenAI-compatible APIs (e.g. DeepSeek). |
| `MaxMessageLength` | `2000` | Max user message length in characters. |
| `MaxRequestsPerIpPerMinute` | `20` | IP rate-limit cap. |
| `SessionTtl` | `2 hours` | Idle session lifetime. |
| `Email` | `null` | SMTP settings for ticket notification emails. `null` disables email. |
| `TicketLanguage` | `"English"` | Language the AI writes ticket summary/details in. Customer-facing replies always match the customer's own language. |

## Customization

### Custom system prompt

Register your own `ISystemPromptBuilder` **before** calling `AddBotWire()` to fully replace the built-in prompt:

```csharp
builder.Services.AddSingleton<ISystemPromptBuilder, MyPromptBuilder>();
builder.Services.AddBotWire(opts => { ... });
```

`ISystemPromptBuilder.Build(documents)` receives the loaded knowledge-base document contents and must return the complete system prompt. Your implementation **must** preserve the control-word contract: the model's first line must be either `ANSWER` or `ESCALATE` on its own line, otherwise escalation and ticket creation stop working.

### Ticket language

Set `TicketLanguage` to any natural-language name the model understands. The AI writes ticket summary and details in that language regardless of what language the customer used:

```csharp
builder.Services.AddBotWire(opts =>
{
    opts.TicketLanguage = "简体中文"; // or "Français", "日本語", etc.
});
```

Customer-facing chat replies are not affected — the bot always replies in the same language the customer wrote in.

### React to created tickets

Hook `OnTicketCreated` to push tickets into your own system (database, queue, CRM) in addition to (or instead of) email:

```csharp
opts.OnTicketCreated = async ticket =>
{
    await db.Tickets.AddAsync(ticket);
    await db.SaveChangesAsync();
};
```

### Custom email template

Register your own `IEmailTemplateFormatter` to control how tickets are formatted in email:

```csharp
builder.Services.AddSingleton<IEmailTemplateFormatter, MyEmailFormatter>();
```

## Running tests

### Unit tests (no API key needed)

```powershell
dotnet test --filter "Category!=RequiresMailpit"
```

### Integration tests — LLM (requires an OpenAI-compatible API key)

Set the provider env vars, then run:

```powershell
# PowerShell — OpenAI (default)
$env:BOTWIRE_TEST_API_KEY = "sk-..."
dotnet test tests/BotWire.AspNetCore.IntegrationTests

# PowerShell — DeepSeek (or any OpenAI-compatible provider)
$env:BOTWIRE_TEST_API_KEY  = "sk-..."
$env:BOTWIRE_TEST_MODEL    = "deepseek-chat"
$env:BOTWIRE_TEST_BASE_URL = "https://api.deepseek.com"
dotnet test tests/BotWire.AspNetCore.IntegrationTests
```

```bash
# bash / CI — OpenAI (default)
export BOTWIRE_TEST_API_KEY=sk-...
dotnet test tests/BotWire.AspNetCore.IntegrationTests
```

| Env var | Default | Description |
|---|---|---|
| `BOTWIRE_TEST_API_KEY` | *(required to run LLM tests)* | API key for the test provider. |
| `BOTWIRE_TEST_MODEL` | `gpt-4o-mini` | Model name. |
| `BOTWIRE_TEST_BASE_URL` | *(unset = standard OpenAI)* | Base URL for OpenAI-compatible providers. |

LLM tests are skipped automatically when `BOTWIRE_TEST_API_KEY` is not set.

### Integration tests — email escalation (requires Mailpit)

Install [Mailpit](https://mailpit.axllent.org/) and start it (SMTP on :1025, web UI on :8025):

```powershell
mailpit
```

Then:

```powershell
$env:BOTWIRE_TEST_API_KEY = "sk-..."
$env:MAILPIT_ENABLED      = "1"
dotnet test tests/BotWire.AspNetCore.IntegrationTests
```

Mailpit tests are tagged `Category=RequiresMailpit` and skipped unless both variables are set.

To exclude Mailpit tests from CI:

```powershell
dotnet test --filter "Category!=RequiresMailpit"
```

## AI provider & responsible use

BotWire does **not** include or provide any AI model or API. You supply your own
OpenAI-compatible API key and account, and your only AI cost is what that
provider charges. When you deploy BotWire:

- Customer messages and your knowledge-base content are sent to the third-party
  LLM provider you configure. You are responsible for that provider's terms,
  pricing, data-processing, and privacy obligations.
- You are responsible for the AI-generated output shown to your customers, and
  for disclosing AI use to end users where required by law.
- BotWire grounds answers in your documents and includes prompt-injection
  defenses, but language-model output can still be wrong. Do not rely on it for
  decisions that require guaranteed accuracy without human review.

### Customer PII

Handling your customers' personal data is **your responsibility**. BotWire ships
a best-effort PII guard (enabled by default) that **blocks** user messages
matching common patterns — email addresses, phone numbers, and credit-card-like
numbers — before they are sent to the AI provider. Add your own patterns via
`PiiGuard.AdditionalPatterns`:

```csharp
builder.Services.AddBotWire(opts =>
{
    opts.PiiGuard.AdditionalPatterns.Add(@"\bACME-\d{6}\b"); // e.g. internal account numbers
});
```

This guard is regex-based and **not exhaustive**: it will not catch every form
of personal data, and it rejects rather than redacts. You must confirm, for your
own jurisdiction and data, that no personal data you are not permitted to share
is sent to your AI provider — for example by tuning the patterns, restricting
your knowledge-base content, and choosing a provider whose data-processing terms
meet your obligations.

## License

BotWire is available under the [AGPL v3](LICENSE).
Commercial licenses are available for proprietary use — see [COMMERCIAL.md](COMMERCIAL.md).
