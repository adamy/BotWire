# BotWire

Embeddable AI customer-support bot for ASP.NET Core. Drop in `AddBotWire()` + `MapBotWire()` and you get chat, escalation, and ticket creation over HTTP/SSE.

## Quick start

```csharp
builder.Services.AddBotWire(opts =>
{
    opts.TopicDescription = "Online store customer support";
    opts.Documents        = ["docs/faq.md"];
    opts.ChatProvider     = new OpenAIProviderOptions { ApiKey = "sk-...", Model = "gpt-4o" };
});

app.UseCors();
app.MapBotWire();
```

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

## License

BotWire is available under the [AGPL v3](LICENSE).
Commercial licenses are available for proprietary use — see [COMMERCIAL.md](COMMERCIAL.md).
