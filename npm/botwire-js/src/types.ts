// Public types for the botwire-js SDK (Layer 1 — pure logic, zero DOM).
// See _discussion/design/decisions/017-frontend-stack.md.

/** Configuration for a {@link BotWireClient}. */
export interface BotWireConfig {
  /**
   * Base API path or absolute URL the server mounted BotWire on, e.g. `/support`
   * or `https://app.example.com/support`. The client appends `/session`, `/chat`
   * and `/chat/stream`. Defaults to `/support`.
   */
  endpoint?: string;
  /** Optional public key, sent as the `X-BotWire-Key` header on every request. */
  publicKey?: string;
  /**
   * Custom `fetch` implementation. Defaults to the global `fetch`. Provide one
   * for environments without a global (older Node) or for testing.
   */
  fetch?: typeof fetch;
}

/** Result of a non-streaming {@link BotWireClient.chat} call. */
export interface BotWireResponse {
  /** Server status, e.g. `Answered`, `NeedHuman`, `TicketCreated`, `Blocked`, `PiiBlocked`, `RateLimited`. */
  status: string;
  /** The assistant message (or status explanation). */
  message: string;
  /** Session token to reuse on the next turn. */
  sessionToken: string;
  /** Ticket id when an escalation produced a ticket. */
  ticketId?: string;
}

/** Result of {@link BotWireClient.initSession}. */
export interface InitSessionResult {
  /** The new session token. */
  sessionToken: string;
  /** `true` when the server wants the user's name before answering. */
  needsName: boolean;
  /** Host-configured error message, surfaced for display on failures. */
  errorMessage?: string;
}

/** A single event yielded by {@link BotWireClient.streamChat}. */
export type BotWireEvent =
  /** A chunk of assistant text. Concatenate `delta`s to render the reply. */
  | { type: 'delta'; delta: string }
  /** The bot needs a contact email; resend via {@link ChatOptions.contactEmail}. */
  | { type: 'collect_contact' }
  /** The turn escalated to a human and a ticket was created. */
  | { type: 'escalated'; ticketId: string; message: string }
  /** The message was blocked (PII, length, prompt-injection, off-topic). */
  | { type: 'blocked'; reason: string }
  /** Terminal event — the stream finished normally. */
  | { type: 'done' };

/** Per-call options for {@link BotWireClient.chat} and {@link BotWireClient.streamChat}. */
export interface ChatOptions {
  /** Contact email, supplied in response to a `collect_contact` event. */
  contactEmail?: string;
  /** Abort signal to cancel the request or stream. */
  signal?: AbortSignal;
}
