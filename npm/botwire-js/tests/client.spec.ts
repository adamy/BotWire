// Tests for BotWireClient (Layer 1): session init, non-stream chat, SSE streaming,
// event mapping, and stale-token self-healing. A custom fetch is injected so the
// suite runs without DOM or network.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { BotWireClient, BotWireError } from '../src/client';
import type { BotWireEvent } from '../src/types';

interface RecordedCall {
  url: string;
  body: Record<string, unknown>;
}

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

function sse(...lines: string[]): Response {
  return new Response(lines.map(l => `data: ${l}\n\n`).join(''), {
    status: 200,
    headers: { 'Content-Type': 'text/event-stream' },
  });
}

async function collect(gen: AsyncGenerator<BotWireEvent>): Promise<BotWireEvent[]> {
  const out: BotWireEvent[] = [];
  for await (const e of gen) out.push(e);
  return out;
}

describe('BotWireClient', () => {
  let calls: RecordedCall[];

  beforeEach(() => {
    calls = [];
  });

  /** Build a client with a fetch stub. `handler` sees the call and a per-path counter. */
  function client(handler: (call: RecordedCall, n: { stream: number; chat: number }) => Response, config = {}) {
    const n = { stream: 0, chat: 0 };
    const fetchStub = vi.fn(async (url: string, init: RequestInit) => {
      const call: RecordedCall = { url, body: JSON.parse((init.body as string) ?? '{}') };
      calls.push(call);
      if (url.endsWith('/chat/stream')) n.stream++;
      else if (url.endsWith('/chat')) n.chat++;
      return handler(call, n);
    });
    return new BotWireClient({ endpoint: '/support', fetch: fetchStub as unknown as typeof fetch, ...config });
  }

  it('initSession adopts the token and reports needsName', async () => {
    const c = client(() => jsonResponse(200, { sessionToken: 'tok-1', needsName: true, errorMessage: 'oops' }));
    const res = await c.initSession();
    expect(res).toEqual({ sessionToken: 'tok-1', needsName: true, errorMessage: 'oops' });
    expect(c.getSessionToken()).toBe('tok-1');
  });

  it('chat auto-creates a session, then sends the message with that token', async () => {
    const c = client(call => {
      if (call.url.endsWith('/session')) return jsonResponse(200, { sessionToken: 'tok-1', needsName: false });
      return jsonResponse(200, { status: 'Answered', message: 'Hi there', sessionToken: 'tok-1' });
    });
    const res = await c.chat('hello');
    expect(res.status).toBe('Answered');
    expect(res.message).toBe('Hi there');
    expect(calls[0]!.url).toContain('/session');
    expect(calls[1]!.url).toMatch(/\/chat$/);
    expect(calls[1]!.body['sessionToken']).toBe('tok-1');
    expect(calls[1]!.body['message']).toBe('hello');
  });

  it('chat forwards contactEmail', async () => {
    const c = client(call =>
      call.url.endsWith('/session')
        ? jsonResponse(200, { sessionToken: 'tok-1' })
        : jsonResponse(200, { status: 'TicketCreated', message: 'done', sessionToken: 'tok-1', ticketId: 'T-1' }));
    const res = await c.chat('', { contactEmail: 'a@b.com' });
    expect(res.ticketId).toBe('T-1');
    expect(calls[1]!.body['contactEmail']).toBe('a@b.com');
  });

  it('streamChat maps wire events to the public shape and ends on done', async () => {
    const c = client(call => {
      if (call.url.endsWith('/session')) return jsonResponse(200, { sessionToken: 'tok-1' });
      return sse(
        '{"type":"token","value":"Hel"}',
        '{"type":"token","value":"lo"}',
        '[DONE]',
      );
    });
    const events = await collect(c.streamChat('hi'));
    expect(events).toEqual([
      { type: 'delta', delta: 'Hel' },
      { type: 'delta', delta: 'lo' },
      { type: 'done' },
    ]);
  });

  it('streamChat maps collect_contact, escalated, and blocked', async () => {
    const c = client(call => {
      if (call.url.endsWith('/session')) return jsonResponse(200, { sessionToken: 'tok-1' });
      return sse(
        '{"type":"collect_contact"}',
        '{"type":"escalated","ticketId":"T-9","message":"We made a ticket"}',
        '{"type":"blocked","reason":"too long"}',
        '[DONE]',
      );
    });
    const events = await collect(c.streamChat('hi'));
    expect(events).toEqual([
      { type: 'collect_contact' },
      { type: 'escalated', ticketId: 'T-9', message: 'We made a ticket' },
      { type: 'blocked', reason: 'too long' },
      { type: 'done' },
    ]);
  });

  it('streamChat self-heals a stale token once and resends the message', async () => {
    const c = client((call, n) => {
      if (call.url.endsWith('/session')) return jsonResponse(200, { sessionToken: 'fresh' });
      return n.stream === 1
        ? jsonResponse(400, { status: 'InvalidSession', message: 'bad token' })
        : sse('{"type":"token","value":"ok"}', '[DONE]');
    });
    c.setSessionToken('stale');
    const events = await collect(c.streamChat('hello'));

    const streamCalls = calls.filter(x => x.url.endsWith('/chat/stream'));
    expect(streamCalls).toHaveLength(2);
    expect(streamCalls[0]!.body['sessionToken']).toBe('stale');
    expect(streamCalls[1]!.body['sessionToken']).toBe('fresh');
    expect(streamCalls[1]!.body['message']).toBe('hello');
    expect(events.at(-1)).toEqual({ type: 'done' });
  });

  it('chat self-heals a stale token once', async () => {
    const c = client((call, n) => {
      if (call.url.endsWith('/session')) return jsonResponse(200, { sessionToken: 'fresh' });
      return n.chat === 1
        ? jsonResponse(400, { status: 'InvalidSession', message: 'bad token' })
        : jsonResponse(200, { status: 'Answered', message: 'recovered', sessionToken: 'fresh' });
    });
    c.setSessionToken('stale');
    const res = await c.chat('hello');
    expect(res.message).toBe('recovered');
    expect(calls.filter(x => x.url.endsWith('/chat'))).toHaveLength(2);
  });

  it('does not retry on a non-InvalidSession 400', async () => {
    const c = client(() => jsonResponse(400, { status: 'Blocked', message: 'nope', sessionToken: 'keep' }));
    c.setSessionToken('keep');
    const res = await c.chat('hello');
    expect(res.status).toBe('Blocked');
    expect(calls.filter(x => x.url.endsWith('/session'))).toHaveLength(0);
    expect(calls.filter(x => x.url.endsWith('/chat'))).toHaveLength(1);
  });

  it('sends X-BotWire-Key when a publicKey is configured', async () => {
    const headerSeen: string[] = [];
    const fetchStub = vi.fn(async (_url: string, init: RequestInit) => {
      headerSeen.push((init.headers as Record<string, string>)['X-BotWire-Key'] ?? '');
      return jsonResponse(200, { sessionToken: 'tok-1' });
    });
    const c = new BotWireClient({ endpoint: '/support', publicKey: 'pk-123', fetch: fetchStub as unknown as typeof fetch });
    await c.initSession();
    expect(headerSeen[0]).toBe('pk-123');
  });

  it('throws BotWireError when streamChat gets a non-OK response', async () => {
    const c = client(call =>
      call.url.endsWith('/session')
        ? jsonResponse(200, { sessionToken: 'tok-1' })
        : jsonResponse(500, { status: 'Error', message: 'server exploded' }));
    await expect(collect(c.streamChat('hi'))).rejects.toMatchObject({
      name: 'BotWireError',
      status: 'Error',
      httpStatus: 500,
    });
    expect(BotWireError).toBeDefined();
  });

  it('strips a trailing slash from the endpoint', async () => {
    const c = client(() => jsonResponse(200, { sessionToken: 'tok-1' }), { endpoint: '/support/' });
    await c.initSession();
    expect(calls[0]!.url).toBe('/support/session');
  });
});
