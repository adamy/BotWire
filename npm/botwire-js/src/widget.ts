// BotWire Web Component — zero-dependency chat widget
// Shadow DOM isolation, SSE streaming via fetch + ReadableStream.

import { BotWireClient, BotWireError } from './client.js';

const STORAGE_KEY = 'botwire_session';

// ── i18n ───────────────────────────────────────────────────────────────────────
// Built-in UI translations. A host-supplied data-* attribute always takes
// precedence over these (see BotWireWidget.t). The `en` table holds the exact
// Phase-1 defaults so en / no data-lang behaviour is unchanged.

type StringKey =
  | 'title' | 'greeting' | 'placeholder' | 'sendLabel'
  | 'contactPrompt' | 'emailPlaceholder' | 'submitLabel' | 'cancelLabel' | 'cancelMessage';

const TRANSLATIONS: Record<string, Record<StringKey, string>> = {
  en: {
    title:            'Support',
    greeting:         'How can we help you today?',
    placeholder:      'Type a message…',
    sendLabel:        'Send',
    contactPrompt:    'Please leave your email address so our team can follow up with you.',
    emailPlaceholder: 'your@email.com',
    submitLabel:      'Submit',
    cancelLabel:      'Cancel',
    cancelMessage:    'You have ended this conversation.',
  },
  'zh-CN': {
    title:            '在线客服',
    greeting:         '请问有什么可以帮您？',
    placeholder:      '输入消息…',
    sendLabel:        '发送',
    contactPrompt:    '请留下您的邮箱，方便我们的团队跟进。',
    emailPlaceholder: 'your@email.com',
    submitLabel:      '提交',
    cancelLabel:      '取消',
    cancelMessage:    '您已结束本次会话。',
  },
  ja: {
    title:            'サポート',
    greeting:         'ご用件をお知らせください。',
    placeholder:      'メッセージを入力…',
    sendLabel:        '送信',
    contactPrompt:    'メールアドレスをご記入いただければ、担当者よりご連絡いたします。',
    emailPlaceholder: 'your@email.com',
    submitLabel:      '送信する',
    cancelLabel:      'キャンセル',
    cancelMessage:    'この会話を終了しました。',
  },
};

/** Normalise a data-lang value to a translation-table key; unknown → 'en'. */
function normaliseLang(lang: string | undefined): string {
  if (!lang) return 'en';
  const l = lang.toLowerCase();
  if (l === 'zh' || l.startsWith('zh-') || l.startsWith('zh_')) return 'zh-CN';
  if (l === 'ja' || l.startsWith('ja-') || l.startsWith('ja_')) return 'ja';
  return 'en';
}

// ── CSS ──────────────────────────────────────────────────────────────────────

function escapeCss(s: string): string {
  return s.replace(/\\/g, '\\\\').replace(/'/g, "\\'");
}

function buildCss(primary: string, position: string, greeting: string): string {
  const isLeft = position === 'bottom-left';
  const side   = isLeft ? 'left' : 'right';
  return `
*,*::before,*::after{box-sizing:border-box;margin:0;padding:0}

#bubble{
  position:fixed;bottom:24px;${side}:24px;
  width:56px;height:56px;
  background:${primary};border:none;border-radius:50%;cursor:pointer;
  display:flex;align-items:center;justify-content:center;
  box-shadow:0 4px 20px rgba(0,0,0,.28);
  transition:transform .2s ease,box-shadow .2s ease;
  z-index:2147483646;color:#fff;
}
#bubble:hover{transform:scale(1.08);box-shadow:0 6px 24px rgba(0,0,0,.32)}
#bubble svg{width:26px;height:26px;fill:#fff;pointer-events:none;flex-shrink:0}

#panel{
  position:fixed;bottom:96px;${side}:20px;
  width:360px;min-height:400px;max-height:560px;
  background:#fff;border-radius:16px;
  box-shadow:0 8px 48px rgba(0,0,0,.18);
  display:flex;flex-direction:column;overflow:hidden;
  z-index:2147483646;
  animation:bw-in .2s ease-out;
}
#panel[hidden]{display:none!important}
@keyframes bw-in{from{transform:scale(.92) translateY(8px);opacity:0}to{transform:none;opacity:1}}

#header{
  display:flex;align-items:center;justify-content:space-between;gap:8px;
  padding:14px 16px;background:${primary};color:#fff;flex-shrink:0;
}
#header-title{font-weight:600;font-size:15px;flex:1}
#header-actions{display:flex;align-items:center;gap:2px}
#reset,#close{
  background:none;border:none;color:#fff;cursor:pointer;
  font-size:20px;line-height:1;padding:2px 6px;border-radius:6px;opacity:.75;
  display:flex;align-items:center;
}
#reset[hidden]{display:none!important}
#reset svg{width:17px;height:17px;fill:#fff}
#reset:hover,#close:hover{opacity:1;background:rgba(255,255,255,.15)}

#messages{
  flex:1;overflow-y:auto;padding:12px 12px 4px;
  display:flex;flex-direction:column;gap:8px;
  scroll-behavior:smooth;
}
#messages:empty::before{
  content:'${escapeCss(greeting)}';
  color:#94a3b8;font-size:13px;text-align:center;
  margin:auto;padding:32px 16px;
}

.msg{
  max-width:82%;padding:10px 14px;border-radius:14px;
  font-size:14px;line-height:1.55;word-break:break-word;
  animation:msg-in .15s ease-out;
}
@keyframes msg-in{from{transform:translateY(5px);opacity:0}to{transform:none;opacity:1}}
.msg-user{align-self:flex-end;background:${primary};color:#fff;border-bottom-right-radius:3px}
.msg-bot {align-self:flex-start;background:#f1f5f9;color:#1e293b;border-bottom-left-radius:3px}
.msg-sys {
  align-self:center;background:#fef9c3;color:#854d0e;
  font-size:13px;border-radius:8px;text-align:center;max-width:90%;
}

#typing{padding:6px 12px 8px;display:flex;gap:4px;align-items:center;flex-shrink:0}
#typing[hidden]{display:none!important}
#typing span{
  width:7px;height:7px;background:#94a3b8;border-radius:50%;
  animation:bw-dot .9s infinite ease-in-out;
}
#typing span:nth-child(2){animation-delay:.15s}
#typing span:nth-child(3){animation-delay:.3s}
@keyframes bw-dot{0%,80%,100%{transform:scale(.6);opacity:.4}40%{transform:scale(1);opacity:1}}

#starters{
  display:flex;flex-wrap:wrap;gap:8px;
  padding:4px 12px 12px;flex-shrink:0;
}
#starters[hidden]{display:none!important}
.starter{
  background:#f1f5f9;color:#334155;border:1px solid #e2e8f0;border-radius:999px;
  padding:7px 14px;cursor:pointer;font:inherit;font-size:13px;
  transition:background .15s,border-color .15s;
}
.starter:hover{background:#e2e8f0;border-color:#cbd5e1}

#input-area{
  display:flex;gap:8px;align-items:flex-end;
  padding:10px 12px;border-top:1px solid #e2e8f0;flex-shrink:0;
}
#input-area[hidden]{display:none!important}
#input{
  flex:1;resize:none;border:1px solid #e2e8f0;border-radius:10px;
  padding:8px 12px;font:inherit;font-size:14px;outline:none;
  max-height:100px;overflow-y:auto;line-height:1.5;
  transition:border-color .15s;
}
#input:focus{border-color:${primary};outline:none}
#send{
  background:${primary};color:#fff;border:none;border-radius:10px;
  padding:9px 16px;cursor:pointer;font-size:14px;font-weight:500;
  white-space:nowrap;flex-shrink:0;transition:opacity .15s;
}
#send:disabled{opacity:.45;cursor:not-allowed}
#send:not(:disabled):hover{opacity:.88}

#contact-form{
  padding:14px 16px 16px;border-top:1px solid #e2e8f0;
  display:flex;flex-direction:column;gap:10px;flex-shrink:0;
}
#contact-form[hidden]{display:none!important}
#contact-form p{font-size:13px;color:#64748b;line-height:1.5}
#email-input{
  border:1px solid #e2e8f0;border-radius:10px;
  padding:9px 12px;font:inherit;font-size:14px;outline:none;
  transition:border-color .15s;
}
#email-input:focus{border-color:${primary}}
#contact-buttons{display:flex;gap:8px}
#contact-submit{
  flex:1;background:${primary};color:#fff;border:none;border-radius:10px;
  padding:10px;cursor:pointer;font-size:14px;font-weight:500;
  transition:opacity .15s;
}
#contact-submit:hover{opacity:.88}
#contact-cancel{
  flex:1;background:#f1f5f9;color:#64748b;border:none;border-radius:10px;
  padding:10px;cursor:pointer;font-size:14px;font-weight:500;
  transition:background .15s;
}
#contact-cancel:hover{background:#e2e8f0}

#ticket-card{
  margin:12px 12px 14px;padding:14px 16px;
  background:#f0fdf4;border:1px solid #bbf7d0;border-radius:12px;
  font-size:13px;color:#166534;text-align:center;line-height:1.55;
  flex-shrink:0;
}
#ticket-card[hidden]{display:none!important}

@media(max-width:480px){
  #panel{bottom:0;left:0;right:0;width:100%;min-height:unset;max-height:100%;
    border-radius:0;border-top-left-radius:16px;border-top-right-radius:16px}
  #bubble{bottom:16px;${side}:16px}
}
`;
}

// ── SVG icons ────────────────────────────────────────────────────────────────

const ICON_CHAT = `<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M20 2H4a2 2 0 00-2 2v18l4-4h14a2 2 0 002-2V4a2 2 0 00-2-2z"/></svg>`;
const ICON_CLOSE_CHAT = `<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M20 2H4a2 2 0 00-2 2v18l4-4h14a2 2 0 002-2V4a2 2 0 00-2-2zM6 10h12v2H6v-2zm0-4h12v2H6V6zm8 8H6v-2h8v2z"/></svg>`;
const ICON_RESET = `<svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path d="M17.65 6.35A7.96 7.96 0 0012 4a8 8 0 108 8h-2a6 6 0 11-1.76-4.24L13 11h7V4l-2.35 2.35z"/></svg>`;

// ── Web Component ─────────────────────────────────────────────────────────────

class BotWireWidget extends HTMLElement {
  private readonly shadow: ShadowRoot;
  private client!:       BotWireClient;

  private streaming      = false;
  private streamAbort: AbortController | null = null;
  private awaitingEmail  = false;
  private ticketCreated  = false;
  private errorOccurred  = false;
  private errorMessage   = 'Something went wrong. Please try again.';

  // DOM refs — assigned in mount(), called before any interaction
  private panel!:     HTMLElement;
  private bubble!:    HTMLButtonElement;
  private messages!:  HTMLElement;
  private startersBox!: HTMLElement;
  private resetBtn!:  HTMLButtonElement;
  private typing!:    HTMLElement;
  private inputArea!: HTMLElement;
  private input!:     HTMLTextAreaElement;
  private sendBtn!:   HTMLButtonElement;
  private contact!:    HTMLFormElement;
  private emailIn!:    HTMLInputElement;
  private cancelBtn!:  HTMLButtonElement;
  private ticket!:     HTMLElement;

  constructor() {
    super();
    this.shadow = this.attachShadow({ mode: 'open' });
  }

  // ── Attribute helpers ───────────────────────────────────────────────────────

  private get endpoint():         string           { return this.dataset['endpoint']     ?? '/support'; }
  private get primary():          string           { return this.dataset['primaryColor'] ?? '#6366f1'; }
  private get position():         string           { return this.dataset['position']     ?? 'bottom-right'; }
  private get publicKey():        string|undefined { return this.dataset['publicKey']; }
  private get offtopicMessage():  string|undefined { return this.dataset['offtopicMessage']; }
  private get resetEnabled():     boolean          { return this.dataset['reset']        !== 'false'; }
  private get resetConfirm():     boolean          { return this.dataset['resetConfirm'] !== 'false'; }

  // Localised UI strings: a host data-* attribute wins, else the data-lang table, else en.
  private get langKey():          string { return normaliseLang(this.dataset['lang']); }
  private t(key: StringKey): string {
    const override = this.dataset[key];
    if (override !== undefined) return override;
    return TRANSLATIONS[this.langKey]?.[key] ?? TRANSLATIONS['en']![key];
  }

  private get widgetTitle():      string { return this.t('title'); }
  private get placeholder():      string { return this.t('placeholder'); }
  private get contactPrompt():    string { return this.t('contactPrompt'); }
  private get emailPlaceholder(): string { return this.t('emailPlaceholder'); }
  private get sendLabel():        string { return this.t('sendLabel'); }
  private get submitLabel():      string { return this.t('submitLabel'); }
  private get cancelLabel():      string { return this.t('cancelLabel'); }
  private get cancelMessage():    string { return this.t('cancelMessage'); }
  private get greeting():         string { return this.t('greeting'); }

  /** Pipe-separated conversation starters, or [] when not configured. */
  private get starters(): string[] {
    return (this.dataset['starters'] ?? '')
      .split('|').map(s => s.trim()).filter(s => s.length > 0);
  }

  // ── Lifecycle ───────────────────────────────────────────────────────────────

  connectedCallback(): void {
    this.mount();
    this.client = new BotWireClient({ endpoint: this.endpoint, publicKey: this.publicKey });
    this.client.setSessionToken(sessionStorage.getItem(STORAGE_KEY));
    if (!this.client.getSessionToken()) void this.initSession();
  }

  // ── Mount ───────────────────────────────────────────────────────────────────

  private mount(): void {
    this.shadow.innerHTML = `
<style>${buildCss(this.primary, this.position, this.greeting)}</style>
<button id="bubble" aria-label="Open support chat" aria-expanded="false">${ICON_CHAT}</button>
<div id="panel" hidden role="dialog" aria-label="${this.esc(this.widgetTitle)} support chat">
  <div id="header">
    <span id="header-title">${this.esc(this.widgetTitle)}</span>
    <div id="header-actions">
      <button id="reset" type="button" hidden aria-label="Reset conversation">${ICON_RESET}</button>
      <button id="close" aria-label="Close chat">✕</button>
    </div>
  </div>
  <div id="messages" role="log" aria-live="polite" aria-relevant="additions"></div>
  <div id="starters" hidden></div>
  <div id="typing" hidden aria-hidden="true"><span></span><span></span><span></span></div>
  <div id="input-area">
    <textarea id="input" placeholder="${this.esc(this.placeholder)}" rows="1" aria-label="Message input"></textarea>
    <button id="send" type="button">${this.esc(this.sendLabel)}</button>
  </div>
  <form id="contact-form" hidden>
    <p>${this.esc(this.contactPrompt)}</p>
    <input type="email" id="email-input" placeholder="${this.esc(this.emailPlaceholder)}" required aria-label="Email address">
    <div id="contact-buttons">
      <button id="contact-submit" type="submit">${this.esc(this.submitLabel)}</button>
      <button id="contact-cancel" type="button">${this.esc(this.cancelLabel)}</button>
    </div>
  </form>
  <div id="ticket-card" hidden role="status"></div>
</div>`;

    this.panel     = this.q('#panel');
    this.bubble    = this.q<HTMLButtonElement>('#bubble');
    this.messages  = this.q('#messages');
    this.startersBox = this.q('#starters');
    this.resetBtn  = this.q<HTMLButtonElement>('#reset');
    this.typing    = this.q('#typing');
    this.inputArea = this.q('#input-area');
    this.input     = this.q<HTMLTextAreaElement>('#input');
    this.sendBtn   = this.q<HTMLButtonElement>('#send');
    this.contact   = this.q<HTMLFormElement>('#contact-form');
    this.emailIn   = this.q<HTMLInputElement>('#email-input');
    this.cancelBtn = this.q<HTMLButtonElement>('#contact-cancel');
    this.ticket    = this.q('#ticket-card');

    this.bubble.addEventListener('click', () => this.toggle());
    this.q('#close').addEventListener('click', () => this.close());
    this.resetBtn.addEventListener('click', () => this.handleReset());
    this.sendBtn.addEventListener('click', () => this.handleSend());
    this.input.addEventListener('keydown', (e: KeyboardEvent) => {
      if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); this.handleSend(); }
    });
    this.input.addEventListener('input', () => this.autoResize());
    this.contact.addEventListener('submit', (e: SubmitEvent) => {
      e.preventDefault();
      this.handleContactSubmit();
    });
    this.cancelBtn.addEventListener('click', () => this.handleContactCancel());

    // Reset button shows by default; a host opts out with data-reset="false".
    this.resetBtn.hidden = !this.resetEnabled;
    this.renderStarters();
  }

  // ── Session init ────────────────────────────────────────────────────────────

  private async initSession(): Promise<void> {
    try {
      const result = await this.client.initSession();
      sessionStorage.setItem(STORAGE_KEY, result.sessionToken);
      if (result.errorMessage) this.errorMessage = result.errorMessage;
    } catch {
      // Will retry on first send
    }
  }

  // ── Panel open/close ────────────────────────────────────────────────────────

  private toggle(): void {
    if (this.panel.hidden) {
      this.open();
    } else if (this.ticketCreated) {
      this.resetConversation();
      this.input.focus();
    } else {
      this.close();
    }
  }

  private open(): void {
    if (this.ticketCreated) this.resetConversation();
    this.panel.hidden = false;
    this.bubble.innerHTML = ICON_CLOSE_CHAT;
    this.bubble.setAttribute('aria-expanded', 'true');
    if (!this.awaitingEmail) this.input.focus();
    else this.emailIn.focus();
  }

  private resetConversation(): void {
    this.streamAbort?.abort();   // cancel any in-flight turn so it can't write into the fresh session
    this.streamAbort = null;
    this.messages.innerHTML = '';
    this.ticketCreated  = false;
    this.awaitingEmail  = false;
    this.streaming      = false;
    this.errorOccurred  = false;
    this.contact.hidden  = true;
    this.ticket.hidden   = true;
    this.inputArea.hidden = false;
    this.sendBtn.disabled = false;
    this.emailIn.value   = '';
    this.client.setSessionToken(null);
    sessionStorage.removeItem(STORAGE_KEY);
    this.renderStarters();
    void this.initSession();
  }

  // ── Reset button ──────────────────────────────────────────────────────────────

  private handleReset(): void {
    if (this.resetConfirm) {
      const msg = this.dataset['resetConfirmMessage'] ?? 'Start a new conversation?';
      if (typeof confirm === 'function' && !confirm(msg)) return;
    }
    this.resetConversation();
    if (!this.panel.hidden && !this.awaitingEmail) this.input.focus();
  }

  // ── Conversation starters ───────────────────────────────────────────────────

  /** (Re)render starter chips into an empty conversation; hide the row when none configured. */
  private renderStarters(): void {
    this.startersBox.innerHTML = '';
    const starters = this.starters;
    if (starters.length === 0) { this.startersBox.hidden = true; return; }

    for (const text of starters) {
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'starter';
      btn.textContent = text;
      btn.addEventListener('click', () => {
        this.input.value = text;
        this.handleSend();
      });
      this.startersBox.appendChild(btn);
    }
    this.startersBox.hidden = false;
  }

  private close(): void {
    if (this.errorOccurred) this.resetConversation();
    this.panel.hidden = true;
    this.bubble.innerHTML = ICON_CHAT;
    this.bubble.setAttribute('aria-expanded', 'false');
  }

  // ── Send / stream ───────────────────────────────────────────────────────────

  private handleSend(): void {
    if (this.streaming || this.awaitingEmail) return;
    const text = this.input.value.trim();
    if (!text) return;
    this.input.value = '';
    this.autoResize();
    this.startersBox.hidden = true; // starters only show on an empty conversation
    this.appendMessage('user', text);
    void this.stream(text);
  }

  private handleContactSubmit(): void {
    const email = this.emailIn.value.trim();
    if (!email) return;
    this.contact.hidden = true;
    this.awaitingEmail  = false;
    void this.stream('', email);
  }

  private handleContactCancel(): void {
    this.contact.hidden = true;
    this.awaitingEmail  = false;
    this.ticketCreated  = true;
    this.appendMessage('sys', this.cancelMessage);
  }

  private async stream(message: string, contactEmail?: string): Promise<void> {
    if (this.streaming) return;
    this.streaming    = true;
    this.sendBtn.disabled = true;
    this.typing.hidden = false;

    const controller = new AbortController();
    this.streamAbort = controller;
    let botEl: HTMLElement | null = null;

    try {
      for await (const evt of this.client.streamChat(message, { contactEmail, signal: controller.signal })) {
        this.typing.hidden = true;

        switch (evt.type) {
          case 'delta':
            if (!botEl) botEl = this.appendMessage('bot', '');
            botEl.textContent += evt.delta;
            this.scrollBottom();
            break;

          case 'collect_contact':
            this.inputArea.hidden = true;
            this.contact.hidden   = false;
            this.awaitingEmail    = true;
            // rAF: wait for layout reflow so messages area has shrunk before scrolling
            requestAnimationFrame(() => { this.scrollBottom(); this.emailIn.focus(); });
            break;

          case 'escalated':
            this.ticketCreated      = true;
            this.ticket.hidden      = false;
            this.ticket.textContent = evt.message;
            this.inputArea.hidden   = true;
            break;

          case 'blocked':
            // In the stream path a `blocked` event is the off-topic guard (PII/length/injection
            // are rejected before streaming). Show it as its own assistant bubble — never reuse a
            // partially-streamed answer bubble. A host can override the wording with
            // data-offtopic-message.
            this.appendMessage('bot', this.offtopicMessage ?? evt.reason);
            break;

          case 'done':
            break;
        }
      }
    } catch (err) {
      // A superseded stream (reset/close aborted it) must not touch the UI of the new turn.
      if (controller.signal.aborted) return;
      this.typing.hidden = true;
      if (err instanceof DOMException && err.name === 'AbortError') {
        // user navigated away / aborted — stay silent
      } else if (err instanceof BotWireError) {
        // server rejected the turn — surface the host-configured message
        this.errorOccurred = true;
        this.appendMessage('sys', this.errorMessage);
      } else {
        this.appendMessage('sys', 'Connection error. Please try again.');
      }
    } finally {
      // Only the active stream owns the shared state; an aborted/superseded one bows out.
      if (this.streamAbort === controller) {
        this.streamAbort = null;
        const token = this.client.getSessionToken();
        if (token) sessionStorage.setItem(STORAGE_KEY, token);
        this.typing.hidden    = true;
        this.streaming        = false;
        if (!this.awaitingEmail && !this.ticketCreated) {
          this.sendBtn.disabled = false;
          if (!this.panel.hidden) this.input.focus();
        }
      }
    }
  }

  // ── DOM helpers ─────────────────────────────────────────────────────────────

  private appendMessage(role: 'user' | 'bot' | 'sys', text: string): HTMLElement {
    const el = document.createElement('div');
    el.className = `msg msg-${role}`;
    el.textContent = text;
    this.messages.appendChild(el);
    this.scrollBottom();
    return el;
  }

  private scrollBottom(): void {
    this.messages.scrollTop = this.messages.scrollHeight;
  }

  private autoResize(): void {
    this.input.style.height = 'auto';
    this.input.style.height = `${Math.min(this.input.scrollHeight, 100)}px`;
  }

  private esc(s: string): string {
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  private q<T extends Element = HTMLElement>(sel: string): T {
    return this.shadow.querySelector<T>(sel)!;
  }
}

customElements.define('botwire-widget', BotWireWidget);
