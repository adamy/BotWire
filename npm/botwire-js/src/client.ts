// BotWireClient — Layer 1 of the frontend stack: framework-agnostic, zero DOM.
// Wraps the BotWire HTTP API (session init, chat, SSE streaming), manages the
// session token, and self-heals a stale token once (the same recovery the
// widget does — see Task 21). The <botwire-widget> Web Component is built on top.

import type {
  BotWireConfig,
  BotWireEvent,
  BotWireResponse,
  ChatOptions,
  InitSessionResult,
} from './types.js';

const DEFAULT_ENDPOINT = '/support';

/** Thrown when a request fails at the transport level or the server returns a non-OK status. */
export class BotWireError extends Error {
  constructor(
    /** Server status string, or `Error` when none was returned. */
    readonly status: string,
    message: string,
    /** HTTP status code. */
    readonly httpStatus: number,
  ) {
    super(message);
    this.name = 'BotWireError';
  }
}

/**
 * Framework-agnostic client for the BotWire support endpoints.
 *
 * ```ts
 * const client = new BotWireClient({ endpoint: '/support' });
 * for await (const e of client.streamChat('How do refunds work?')) {
 *   if (e.type === 'delta')     output += e.delta;
 *   if (e.type === 'escalated') showTicket(e.ticketId);
 * }
 * ```
 */
export class BotWireClient {
  private readonly endpoint: string;
  private readonly publicKey?: string;
  private readonly _fetch: typeof fetch;
  private _sessionToken: string | null = null;

  constructor(config: BotWireConfig = {}) {
    this.endpoint = stripTrailingSlashes(config.endpoint ?? DEFAULT_ENDPOINT);
    this.publicKey = config.publicKey;
    const f = config.fetch ?? globalThis.fetch;
    if (!f) throw new Error('BotWireClient: no global fetch available — pass config.fetch');
    this._fetch = f.bind(globalThis);
  }

  /** The current session token, or `null` before the first session is created. */
  getSessionToken(): string | null {
    return this._sessionToken;
  }

  /** Restore or override the session token (e.g. one persisted from a previous visit). */
  setSessionToken(token: string | null): void {
    this._sessionToken = token;
  }

  /** Create a fresh server session and adopt its token. */
  async initSession(signal?: AbortSignal): Promise<InitSessionResult> {
    const resp = await this.post(`${this.endpoint}/session`, {}, signal);
    if (!resp.ok) throw await this.toError(resp);
    const data = (await resp.json()) as {
      sessionToken: string;
      needsName?: boolean;
      errorMessage?: string;
    };
    this._sessionToken = data.sessionToken;
    return {
      sessionToken: data.sessionToken,
      needsName: data.needsName ?? false,
      errorMessage: data.errorMessage,
    };
  }

  /**
   * Non-streaming chat. Resolves with the full response; inspect `status` to
   * branch (e.g. `NeedHuman`, `TicketCreated`, `Blocked`). Creates a session if
   * none exists and retries once on a stale token.
   */
  async chat(message: string, opts: ChatOptions = {}): Promise<BotWireResponse> {
    await this.ensureSession(opts.signal);

    let resp = await this.post(`${this.endpoint}/chat`, this.body(message, opts), opts.signal);
    if (await this.staleSession(resp)) {
      await this.initSession(opts.signal);
      resp = await this.post(`${this.endpoint}/chat`, this.body(message, opts), opts.signal);
    }

    let data: BotWireResponse;
    try {
      data = (await resp.json()) as BotWireResponse;
    } catch {
      throw await this.toError(resp);
    }
    if (data.sessionToken) this._sessionToken = data.sessionToken;
    return data;
  }

  /**
   * Streaming chat. Yields {@link BotWireEvent}s until a terminal `done` event.
   * Creates a session if none exists and retries once on a stale token.
   */
  async *streamChat(message: string, opts: ChatOptions = {}): AsyncGenerator<BotWireEvent, void, unknown> {
    await this.ensureSession(opts.signal);

    let resp = await this.post(`${this.endpoint}/chat/stream`, this.body(message, opts), opts.signal);
    if (await this.staleSession(resp)) {
      await this.initSession(opts.signal);
      resp = await this.post(`${this.endpoint}/chat/stream`, this.body(message, opts), opts.signal);
    }
    if (!resp.ok || !resp.body) throw await this.toError(resp);

    yield* this.parseSse(resp.body);
  }

  // ── internals ──────────────────────────────────────────────────────────────

  private async ensureSession(signal?: AbortSignal): Promise<void> {
    if (!this._sessionToken) await this.initSession(signal);
  }

  private body(message: string, opts: ChatOptions): Record<string, unknown> {
    const b: Record<string, unknown> = { message, sessionToken: this._sessionToken };
    if (opts.contactEmail) b['contactEmail'] = opts.contactEmail;
    return b;
  }

  /** A 400 with `{ status: 'InvalidSession' }` means the server dropped our token. */
  private async staleSession(resp: Response): Promise<boolean> {
    if (resp.status !== 400) return false;
    try {
      const data = (await resp.clone().json()) as { status?: string };
      if (data.status === 'InvalidSession') {
        this._sessionToken = null;
        return true;
      }
    } catch {
      /* non-JSON body — not a session problem */
    }
    return false;
  }

  private async *parseSse(stream: ReadableStream<Uint8Array>): AsyncGenerator<BotWireEvent, void, unknown> {
    const reader = stream.getReader();
    const decoder = new TextDecoder();
    let buf = '';
    try {
      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        buf += decoder.decode(value, { stream: true });

        let nl: number;
        while ((nl = buf.indexOf('\n')) !== -1) {
          const line = buf.slice(0, nl);
          buf = buf.slice(nl + 1);
          if (!line.startsWith('data: ')) continue;
          const data = line.slice(6);
          if (data === '[DONE]') {
            yield { type: 'done' };
            return;
          }
          const evt = mapWireEvent(data);
          if (evt) yield evt;
        }
      }
    } finally {
      reader.releaseLock();
    }
  }

  private post(url: string, body: Record<string, unknown>, signal?: AbortSignal): Promise<Response> {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (this.publicKey) headers['X-BotWire-Key'] = this.publicKey;
    return this._fetch(url, { method: 'POST', headers, body: JSON.stringify(body), signal });
  }

  private async toError(resp: Response): Promise<BotWireError> {
    let status = 'Error';
    let message = `BotWire request failed (HTTP ${resp.status})`;
    try {
      const d = (await resp.clone().json()) as { status?: string; message?: string };
      if (d.status) status = d.status;
      if (d.message) message = d.message;
    } catch {
      /* keep defaults */
    }
    return new BotWireError(status, message, resp.status);
  }
}

/**
 * Strip trailing slashes in linear time. A regex like `/\/+$/` triggers CodeQL's
 * polynomial-redos rule (quadratic backtracking on a long run of slashes), so we scan
 * from the end instead.
 */
function stripTrailingSlashes(s: string): string {
  let end = s.length;
  while (end > 0 && s.charCodeAt(end - 1) === 47 /* '/' */) end--;
  return s.slice(0, end);
}

/** Map a raw server SSE event to the SDK's public event shape. Returns `null` to skip. */
function mapWireEvent(data: string): BotWireEvent | null {
  let w: { type?: string; value?: string; ticketId?: string; message?: string; reason?: string };
  try {
    w = JSON.parse(data);
  } catch {
    return null;
  }
  switch (w.type) {
    case 'token':
      return { type: 'delta', delta: w.value ?? '' };
    case 'collect_contact':
      return { type: 'collect_contact' };
    case 'escalated':
      return { type: 'escalated', ticketId: w.ticketId ?? '', message: w.message ?? '' };
    case 'blocked':
      return { type: 'blocked', reason: w.reason ?? '' };
    default:
      return null;
  }
}
