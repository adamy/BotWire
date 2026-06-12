# botwire-js

Framework-agnostic JavaScript/TypeScript client for the [BotWire](https://github.com/adamy/BotWire) support API. Zero DOM, zero dependencies — wraps session init, chat, and SSE streaming so React / Vue / Angular / Blazor apps can build their own UI on top.

This is **Layer 1** of the BotWire frontend stack. The `<botwire-widget>` Web Component is Layer 2, built on this client.

## Install

```bash
npm install botwire-js
```

## Usage

```ts
import { BotWireClient } from 'botwire-js';

const client = new BotWireClient({ endpoint: '/support' });

// Streaming
let reply = '';
for await (const e of client.streamChat('How do refunds work?')) {
  switch (e.type) {
    case 'delta':           reply += e.delta; break;
    case 'collect_contact': askForEmail();    break;   // resend with { contactEmail }
    case 'escalated':       showTicket(e.ticketId, e.message); break;
    case 'blocked':         warn(e.reason);   break;
    case 'done':            break;
  }
}
```

```ts
// Non-streaming
const res = await client.chat('How do refunds work?');
if (res.status === 'Answered') render(res.message);
```

The client creates a session automatically on the first call and reuses the token. It self-heals a stale token once (server `400 InvalidSession`, e.g. after an app-pool restart) by rebuilding the session and resending the message — transparent to your code.

### Submitting a contact email

When a turn yields `collect_contact`, ask the user for an email and resend:

```ts
for await (const e of client.streamChat('', { contactEmail: 'user@example.com' })) { /* ... */ }
```

## Configuration

```ts
new BotWireClient({
  endpoint: '/support',   // base path or absolute URL the server mounted BotWire on (default '/support')
  publicKey: 'pk_...',    // optional; sent as the X-BotWire-Key header
  fetch: customFetch,     // optional; defaults to global fetch
});
```

> `endpoint` is the **base** path. The client appends `/session`, `/chat`, and `/chat/stream`.

## API

| Member | Returns | Notes |
|--------|---------|-------|
| `new BotWireClient(config?)` | — | See configuration above. |
| `initSession(signal?)` | `Promise<InitSessionResult>` | Create a session explicitly. Called for you on first chat. |
| `chat(message, opts?)` | `Promise<BotWireResponse>` | Non-streaming turn. |
| `streamChat(message, opts?)` | `AsyncGenerator<BotWireEvent>` | Streaming turn; yields until `done`. |
| `getSessionToken()` | `string \| null` | Current token. |
| `setSessionToken(token)` | `void` | Restore a token (e.g. from storage). |

`opts`: `{ contactEmail?: string; signal?: AbortSignal }`.

### Events (`streamChat`)

| `type` | Payload | Meaning |
|--------|---------|---------|
| `delta` | `delta: string` | A chunk of assistant text. |
| `collect_contact` | — | Bot needs a contact email. |
| `escalated` | `ticketId`, `message` | Escalated to a human; ticket created. |
| `blocked` | `reason` | Message blocked (PII, length, prompt-injection, off-topic). |
| `done` | — | Terminal event. |

Errors throw `BotWireError` (`{ status, message, httpStatus }`).

## Build outputs

ESM (`dist/index.js`), CommonJS (`dist/index.cjs`), and type declarations (`dist/index.d.ts`).

## License

AGPL-3.0-or-later. Commercial licensing: see the [BotWire repository](https://github.com/adamy/BotWire).
