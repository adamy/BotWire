// Tests for widget session self-healing: a 400 with status "InvalidSession"
// must transparently rebuild the session and resend the message exactly once.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import '../src/widget';

const STORAGE_KEY = 'botwire_session';

interface RecordedCall {
  url: string;
  body: Record<string, unknown>;
}

function sseResponse(text: string): Response {
  const payload =
    `data: {"type":"token","value":${JSON.stringify(text)}}\n\n` +
    `data: [DONE]\n\n`;
  return new Response(payload, {
    status: 200,
    headers: { 'Content-Type': 'text/event-stream' },
  });
}

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

function mountWidget(): HTMLElement {
  const el = document.createElement('botwire-widget');
  document.body.appendChild(el);
  return el;
}

function sendMessage(el: HTMLElement, text: string): void {
  const shadow = el.shadowRoot!;
  shadow.querySelector<HTMLButtonElement>('#bubble')!.click();
  const input = shadow.querySelector<HTMLTextAreaElement>('#input')!;
  input.value = text;
  shadow.querySelector<HTMLButtonElement>('#send')!.click();
}

function messagesOf(el: HTMLElement, selector: string): string[] {
  return [...el.shadowRoot!.querySelectorAll(selector)].map(m => m.textContent ?? '');
}

async function streamFinished(el: HTMLElement): Promise<void> {
  await vi.waitFor(() => {
    expect((el as unknown as { streaming: boolean }).streaming).toBe(false);
  });
}

describe('widget session self-healing', () => {
  let calls: RecordedCall[];

  beforeEach(() => {
    document.body.innerHTML = '';
    sessionStorage.clear();
    calls = [];
  });

  function stubFetch(handler: (call: RecordedCall, streamCalls: number) => Response): void {
    let streamCalls = 0;
    vi.stubGlobal('fetch', vi.fn(async (url: string, init: RequestInit) => {
      const call: RecordedCall = {
        url,
        body: JSON.parse((init.body as string) ?? '{}') as Record<string, unknown>,
      };
      calls.push(call);
      if (url.endsWith('/chat/stream')) streamCalls++;
      return handler(call, streamCalls);
    }));
  }

  it('rebuilds the session and resends the message once on InvalidSession', async () => {
    sessionStorage.setItem(STORAGE_KEY, 'stale-token');
    stubFetch((call, streamCalls) => {
      if (call.url.endsWith('/session'))
        return jsonResponse(200, { sessionToken: 'fresh-token', needsName: true });
      return streamCalls === 1
        ? jsonResponse(400, { status: 'InvalidSession', message: 'Invalid session token.', sessionToken: '' })
        : sseResponse('Hi! How can I help?');
    });

    const el = mountWidget();
    sendMessage(el, 'Hello');
    await streamFinished(el);

    const streamBodies = calls.filter(c => c.url.endsWith('/chat/stream'));
    expect(streamBodies).toHaveLength(2);
    expect(streamBodies[0]!.body['sessionToken']).toBe('stale-token');
    expect(streamBodies[1]!.body['sessionToken']).toBe('fresh-token');
    expect(streamBodies[1]!.body['message']).toBe('Hello');
    expect(sessionStorage.getItem(STORAGE_KEY)).toBe('fresh-token');

    // User sees the bot answer, never an error
    expect(messagesOf(el, '.msg-bot')).toEqual(['Hi! How can I help?']);
    expect(messagesOf(el, '.msg-sys')).toEqual([]);
  });

  it('retries at most once, then surfaces the error', async () => {
    sessionStorage.setItem(STORAGE_KEY, 'stale-token');
    stubFetch(call => {
      if (call.url.endsWith('/session'))
        return jsonResponse(200, { sessionToken: 'fresh-token', needsName: true });
      return jsonResponse(400, { status: 'InvalidSession', message: 'Invalid session token.', sessionToken: '' });
    });

    const el = mountWidget();
    sendMessage(el, 'Hello');
    await streamFinished(el);

    expect(calls.filter(c => c.url.endsWith('/chat/stream'))).toHaveLength(2);
    expect(messagesOf(el, '.msg-sys')).toHaveLength(1);
  });

  it('does not retry or clear the token on other 400 responses', async () => {
    sessionStorage.setItem(STORAGE_KEY, 'valid-token');
    stubFetch(() =>
      jsonResponse(400, { status: 'Blocked', message: 'Message too long.', sessionToken: 'valid-token' }));

    const el = mountWidget();
    sendMessage(el, 'Hello');
    await streamFinished(el);

    expect(calls.filter(c => c.url.endsWith('/chat/stream'))).toHaveLength(1);
    expect(calls.filter(c => c.url.endsWith('/session'))).toHaveLength(0);
    expect(sessionStorage.getItem(STORAGE_KEY)).toBe('valid-token');
    expect(messagesOf(el, '.msg-sys')).toHaveLength(1);
  });
});
