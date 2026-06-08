# BasicEmail Sample

Minimal end-to-end demo: BotWire AI chat + email escalation.

## Prerequisites

- .NET 10 SDK
- [Mailpit](https://mailpit.axllent.org/) running on localhost:1025 / localhost:8025
- An OpenAI-compatible API key set in environment variables

## Environment variables

| Variable | Required | Default | Description |
|---|---|---|---|
| `BOTWIRE_TEST_API_KEY` | Yes | — | API key |
| `BOTWIRE_TEST_MODEL` | No | `gpt-4o-mini` | Model name |
| `BOTWIRE_TEST_BASE_URL` | No | *(OpenAI)* | Base URL for OpenAI-compatible providers |

```powershell
# OpenAI
$env:BOTWIRE_TEST_API_KEY = "sk-..."

# OpenAI with a specific model
$env:BOTWIRE_TEST_API_KEY = "sk-..."
$env:BOTWIRE_TEST_MODEL   = "gpt-4o"

# OpenAI-compatible provider (e.g. DeepSeek)
$env:BOTWIRE_TEST_API_KEY  = "sk-..."
$env:BOTWIRE_TEST_MODEL    = "deepseek-chat"
$env:BOTWIRE_TEST_BASE_URL = "https://api.deepseek.com"
```

## Run

```powershell
# Start Mailpit (Docker)
docker run -d -p 1025:1025 -p 8025:8025 axllent/mailpit

# Run the sample
dotnet run --project samples/BasicEmail
```

Open [http://localhost:5000](http://localhost:5000) in your browser.

## Demo flow

1. Click the chat bubble (bottom-right).
2. Ask a support question — e.g. *"What is your return policy?"*
3. Ask something out-of-scope — e.g. *"What time does the Sydney store open?"*
4. The bot will say it doesn't have that information and ask for your email.
5. Enter an email address — a support ticket is created.
6. Check [http://localhost:8025](http://localhost:8025) to see the ticket email in Mailpit.
