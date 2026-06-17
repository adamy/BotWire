# RedisShop — Redis + JS SDK testbed

A two-part sample that exercises BotWire's Redis-backed stores (Tasks 33 & 34) and the
`botwire-js` SDK with a custom React chatbox.

- **`api/`** — ASP.NET Core minimal API. Sample Acme Store product catalog (`/api/products`)
  plus the full BotWire support stack wired to Redis via `.AddBotWireRedis(...)`:
  conversation sessions and distributed rate-limit counters live in Redis.
- **`web/`** — Vite + React + TypeScript shopfront. Renders the catalog and a hand-built
  chat widget on top of the headless `BotWireClient` from `botwire-js` (no Web Component).

## Prerequisites

- .NET 10 SDK
- Node 20+
- **Redis** reachable at `localhost:6379` (e.g. via Docker Desktop):

  ```sh
  docker run -d --name botwire-redis -p 6379:6379 redis:7
  ```

- **Mailpit** (optional) to view escalation ticket emails — SMTP on 1025, UI on 8025:

  ```sh
  docker run -d --name mailpit -p 1025:1025 -p 8025:8025 axllent/mailpit
  ```

  The "Test Escalation" questions in the UI trigger the ticket flow; captured emails
  appear at http://localhost:8025.

- An OpenAI-compatible API key, exported as the same env vars the `BasicEmail` sample uses:

  ```sh
  export BOTWIRE_TEST_API_KEY=sk-...        # required
  export BOTWIRE_TEST_MODEL=gpt-4o-mini     # optional (default)
  export BOTWIRE_TEST_BASE_URL=...          # optional (OpenAI-compatible endpoint)
  export BOTWIRE_REDIS=localhost:6379       # optional (default)
  ```

## Run

Two terminals.

**API** (http://localhost:5180):

```sh
cd samples/RedisShop/api
dotnet run
```

**Web** (http://localhost:5173 — proxies `/api` and `/support` to the API):

```sh
cd samples/RedisShop/web
npm install
npm run dev
```

Open http://localhost:5173, browse products, and use the 💬 button to chat.

## What to test

- **Session persistence (Task 33):** start a conversation, then restart the API
  (`Ctrl+C` and `dotnet run` again). Continue chatting — the session and history survive
  because they live in Redis, not the API process. Inspect with
  `redis-cli KEYS "botwire:session:*"`.
- **Distributed rate limiting (Task 34):** the API caps `MaxMessagesPerMinute = 5`. Send
  6+ messages quickly and watch the 6th get throttled. Counters live under
  `redis-cli KEYS "botwire:rl:*"` with TTLs (`redis-cli TTL <key>`).
