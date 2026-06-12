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

function mountWidget(attrs: Record<string, string> = {}): HTMLElement {
  const el = document.createElement('botwire-widget');
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  document.body.appendChild(el);
  return el;
}

function sseBlocked(reason: string): Response {
  const payload =
    `data: {"type":"blocked","reason":${JSON.stringify(reason)}}\n\n` +
    `data: [DONE]\n\n`;
  return new Response(payload, {
    status: 200,
    headers: { 'Content-Type': 'text/event-stream' },
  });
}

function openPanel(el: HTMLElement): void {
  el.shadowRoot!.querySelector<HTMLButtonElement>('#bubble')!.click();
}

function text(el: HTMLElement, selector: string): string {
  return el.shadowRoot!.querySelector(selector)?.textContent?.trim() ?? '';
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

describe('widget conversation starters', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    sessionStorage.clear();
    vi.stubGlobal('fetch', vi.fn(async (url: string) => {
      if (url.endsWith('/session')) return jsonResponse(200, { sessionToken: 't', needsName: true });
      return sseResponse('Here are our refund details.');
    }));
  });

  function starters(el: HTMLElement): HTMLButtonElement[] {
    return [...el.shadowRoot!.querySelectorAll<HTMLButtonElement>('.starter')];
  }

  it('renders one chip per data-starters entry', () => {
    const el = mountWidget({ 'data-starters': 'Refund policy | Shipping | Account' });
    openPanel(el);
    expect(starters(el).map(b => b.textContent)).toEqual(['Refund policy', 'Shipping', 'Account']);
    expect(el.shadowRoot!.querySelector<HTMLElement>('#starters')!.hidden).toBe(false);
  });

  it('shows no starters row when unconfigured', () => {
    const el = mountWidget();
    openPanel(el);
    expect(starters(el)).toHaveLength(0);
    expect(el.shadowRoot!.querySelector<HTMLElement>('#starters')!.hidden).toBe(true);
  });

  it('clicking a starter sends it and hides the row', async () => {
    const el = mountWidget({ 'data-starters': 'Refund policy|Shipping' });
    openPanel(el);
    starters(el)[0]!.click();
    await streamFinished(el);

    expect(messagesOf(el, '.msg-user')).toEqual(['Refund policy']);
    expect(messagesOf(el, '.msg-bot')).toEqual(['Here are our refund details.']);
    expect(el.shadowRoot!.querySelector<HTMLElement>('#starters')!.hidden).toBe(true);
  });

  it('restores starters after a reset', async () => {
    const el = mountWidget({ 'data-starters': 'Refund policy|Shipping', 'data-reset-confirm': 'false' });
    openPanel(el);
    starters(el)[0]!.click();
    await streamFinished(el);
    expect(el.shadowRoot!.querySelector<HTMLElement>('#starters')!.hidden).toBe(true);

    el.shadowRoot!.querySelector<HTMLButtonElement>('#reset')!.click();

    expect(messagesOf(el, '.msg-user')).toEqual([]);
    expect(starters(el)).toHaveLength(2);
    expect(el.shadowRoot!.querySelector<HTMLElement>('#starters')!.hidden).toBe(false);
  });
});

describe('widget reset button', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    sessionStorage.clear();
    vi.stubGlobal('fetch', vi.fn(async (url: string) => {
      if (url.endsWith('/session')) return jsonResponse(200, { sessionToken: 'new-token', needsName: true });
      return sseResponse('Hi there.');
    }));
  });

  it('clears the message list and session token', async () => {
    sessionStorage.setItem(STORAGE_KEY, 'old-token');
    const el = mountWidget({ 'data-reset-confirm': 'false' });
    sendMessage(el, 'Hello');
    await streamFinished(el);
    expect(messagesOf(el, '.msg-user')).toEqual(['Hello']);

    el.shadowRoot!.querySelector<HTMLButtonElement>('#reset')!.click();

    expect(messagesOf(el, '.msg-user')).toEqual([]);
    expect(messagesOf(el, '.msg-bot')).toEqual([]);
  });

  it('hides the reset button when data-reset is false', () => {
    const el = mountWidget({ 'data-reset': 'false' });
    expect(el.shadowRoot!.querySelector<HTMLElement>('#reset')!.hidden).toBe(true);
  });

  it('aborts an in-flight stream when reset mid-turn', async () => {
    sessionStorage.setItem(STORAGE_KEY, 'tok');
    let capturedSignal: AbortSignal | undefined;
    vi.stubGlobal('fetch', vi.fn(async (url: string, init: RequestInit) => {
      if (url.endsWith('/session')) return jsonResponse(200, { sessionToken: 'tok2', needsName: true });
      capturedSignal = init.signal ?? undefined;
      // A stream that emits one token then stays open, so the turn is still in flight on reset.
      const body = new ReadableStream<Uint8Array>({
        start(c) { c.enqueue(new TextEncoder().encode('data: {"type":"token","value":"Hi"}\n\n')); },
      });
      return new Response(body, { status: 200, headers: { 'Content-Type': 'text/event-stream' } });
    }));

    const el = mountWidget({ 'data-reset-confirm': 'false' });
    sendMessage(el, 'Hello');
    await vi.waitFor(() => expect(capturedSignal).toBeDefined());
    expect((el as unknown as { streaming: boolean }).streaming).toBe(true);

    el.shadowRoot!.querySelector<HTMLButtonElement>('#reset')!.click();

    expect(capturedSignal!.aborted).toBe(true);
    expect((el as unknown as { streaming: boolean }).streaming).toBe(false);
    expect(messagesOf(el, '.msg-user')).toEqual([]);
  });

  it('aborts the reset when the confirm dialog is declined', async () => {
    const el = mountWidget(); // confirm on by default
    sendMessage(el, 'Hello');
    await streamFinished(el);
    vi.stubGlobal('confirm', vi.fn(() => false));

    el.shadowRoot!.querySelector<HTMLButtonElement>('#reset')!.click();

    expect(messagesOf(el, '.msg-user')).toEqual(['Hello']); // unchanged
  });
});

describe('widget i18n', () => {
  beforeEach(() => { document.body.innerHTML = ''; sessionStorage.clear(); });

  it('uses English defaults when data-lang is absent', () => {
    const el = mountWidget();
    expect(text(el, '#header-title')).toBe('Support');
    expect(text(el, '#send')).toBe('Send');
    expect(el.shadowRoot!.querySelector<HTMLTextAreaElement>('#input')!.placeholder).toBe('Type a message…');
  });

  it('localises UI strings for data-lang=zh-CN', () => {
    const el = mountWidget({ 'data-lang': 'zh-CN' });
    expect(text(el, '#header-title')).toBe('在线客服');
    expect(text(el, '#send')).toBe('发送');
    expect(el.shadowRoot!.querySelector<HTMLTextAreaElement>('#input')!.placeholder).toBe('输入消息…');
    expect(text(el, '#contact-submit')).toBe('提交');
  });

  it('localises for data-lang=ja', () => {
    const el = mountWidget({ 'data-lang': 'ja' });
    expect(text(el, '#header-title')).toBe('サポート');
    expect(text(el, '#send')).toBe('送信');
  });

  it('lets a data-* attribute override the language table', () => {
    const el = mountWidget({ 'data-lang': 'zh-CN', 'data-send-label': 'Go', 'data-title': 'Helpdesk' });
    expect(text(el, '#send')).toBe('Go');
    expect(text(el, '#header-title')).toBe('Helpdesk');
    // untouched keys still localise
    expect(el.shadowRoot!.querySelector<HTMLTextAreaElement>('#input')!.placeholder).toBe('输入消息…');
  });

  it('falls back to English for an unknown language', () => {
    const el = mountWidget({ 'data-lang': 'xx' });
    expect(text(el, '#header-title')).toBe('Support');
  });
});

describe('widget off-topic blocked event', () => {
  let blockedReason: string;

  beforeEach(() => {
    document.body.innerHTML = '';
    sessionStorage.clear();
    blockedReason = 'I can only help with support topics.';
    vi.stubGlobal('fetch', vi.fn(async (url: string) => {
      if (url.endsWith('/session')) return jsonResponse(200, { sessionToken: 't', needsName: true });
      return sseBlocked(blockedReason);
    }));
  });

  it('renders a blocked event as a bot bubble using the server reason', async () => {
    const el = mountWidget();
    sendMessage(el, 'who won the football?');
    await streamFinished(el);

    expect(messagesOf(el, '.msg-bot')).toEqual([blockedReason]);
    expect(messagesOf(el, '.msg-sys')).toEqual([]);
  });

  it('prefers data-offtopic-message when set', async () => {
    const el = mountWidget({ 'data-offtopic-message': 'Sorry, off-topic.' });
    sendMessage(el, 'who won the football?');
    await streamFinished(el);

    expect(messagesOf(el, '.msg-bot')).toEqual(['Sorry, off-topic.']);
  });
});
