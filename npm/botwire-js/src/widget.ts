// BotWire Web Component — zero-dependency chat widget
// Shadow DOM isolation, SSE streaming via fetch + ReadableStream.

const STORAGE_KEY = 'botwire_session';

type SseEvent =
  | { type: 'token';           value: string }
  | { type: 'collect_contact'                }
  | { type: 'escalated';       ticketId: string; message: string }
  | { type: 'blocked';         reason: string };

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
#header-title{font-weight:600;font-size:15px}
#close{
  background:none;border:none;color:#fff;cursor:pointer;
  font-size:20px;line-height:1;padding:2px 6px;border-radius:6px;opacity:.75;
}
#close:hover{opacity:1;background:rgba(255,255,255,.15)}

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

// ── Web Component ─────────────────────────────────────────────────────────────

class BotWireWidget extends HTMLElement {
  private readonly shadow: ShadowRoot;

  private sessionToken: string | null = null;
  private streaming      = false;
  private awaitingEmail  = false;
  private ticketCreated  = false;

  // DOM refs — assigned in mount(), called before any interaction
  private panel!:     HTMLElement;
  private bubble!:    HTMLButtonElement;
  private messages!:  HTMLElement;
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

  private get endpoint():         string           { return this.dataset['endpoint']         ?? '/support'; }
  private get widgetTitle():      string           { return this.dataset['title']            ?? 'Support'; }
  private get primary():          string           { return this.dataset['primaryColor']     ?? '#6366f1'; }
  private get position():         string           { return this.dataset['position']         ?? 'bottom-right'; }
  private get publicKey():        string|undefined { return this.dataset['publicKey']; }
  private get placeholder():      string           { return this.dataset['placeholder']      ?? 'Type a message…'; }
  private get contactPrompt():    string           { return this.dataset['contactPrompt']    ?? 'Please leave your email address so our team can follow up with you.'; }
  private get emailPlaceholder(): string           { return this.dataset['emailPlaceholder'] ?? 'your@email.com'; }
  private get sendLabel():        string           { return this.dataset['sendLabel']        ?? 'Send'; }
  private get submitLabel():      string           { return this.dataset['submitLabel']      ?? 'Submit'; }
  private get cancelLabel():      string           { return this.dataset['cancelLabel']      ?? 'Cancel'; }
  private get cancelMessage():    string           { return this.dataset['cancelMessage']    ?? 'You have ended this conversation.'; }
  private get greeting():         string           { return this.dataset['greeting']         ?? 'How can we help you today?'; }

  // ── Lifecycle ───────────────────────────────────────────────────────────────

  connectedCallback(): void {
    this.mount();
    this.sessionToken = sessionStorage.getItem(STORAGE_KEY);
    if (!this.sessionToken) void this.initSession();
  }

  // ── Mount ───────────────────────────────────────────────────────────────────

  private mount(): void {
    this.shadow.innerHTML = `
<style>${buildCss(this.primary, this.position, this.greeting)}</style>
<button id="bubble" aria-label="Open support chat" aria-expanded="false">${ICON_CHAT}</button>
<div id="panel" hidden role="dialog" aria-label="${this.esc(this.widgetTitle)} support chat">
  <div id="header">
    <span id="header-title">${this.esc(this.widgetTitle)}</span>
    <button id="close" aria-label="Close chat">✕</button>
  </div>
  <div id="messages" role="log" aria-live="polite" aria-relevant="additions"></div>
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
  }

  // ── Session init ────────────────────────────────────────────────────────────

  private async initSession(): Promise<void> {
    try {
      const resp = await this.post(`${this.endpoint}/session`, {});
      if (resp.ok) {
        const data = await resp.json() as { sessionToken: string };
        this.sessionToken = data.sessionToken;
        sessionStorage.setItem(STORAGE_KEY, data.sessionToken);
      }
    } catch {
      // Will retry on first send
    }
  }

  // ── Panel open/close ────────────────────────────────────────────────────────

  private toggle(): void {
    console.log('[BotWire] toggle — panel.hidden:', this.panel.hidden, 'ticketCreated:', this.ticketCreated);
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
    console.log('[BotWire] open — ticketCreated:', this.ticketCreated);
    if (this.ticketCreated) this.resetConversation();
    this.panel.hidden = false;
    this.bubble.innerHTML = ICON_CLOSE_CHAT;
    this.bubble.setAttribute('aria-expanded', 'true');
    if (!this.awaitingEmail) this.input.focus();
    else this.emailIn.focus();
  }

  private resetConversation(): void {
    console.log('[BotWire] resetConversation called — ticketCreated:', this.ticketCreated, 'panel.hidden:', this.panel.hidden);
    this.messages.innerHTML = '';
    this.ticketCreated  = false;
    this.awaitingEmail  = false;
    this.streaming      = false;
    this.contact.hidden  = true;
    this.ticket.hidden   = true;
    this.inputArea.hidden = false;
    this.sendBtn.disabled = false;
    this.emailIn.value   = '';
    this.sessionToken    = null;
    sessionStorage.removeItem(STORAGE_KEY);
    void this.initSession();
    console.log('[BotWire] resetConversation done — inputArea.hidden:', this.inputArea.hidden);
  }

  private close(): void {
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

    if (!this.sessionToken) await this.initSession();

    const body: Record<string, unknown> = { message, sessionToken: this.sessionToken };
    if (contactEmail) body['contactEmail'] = contactEmail;

    let botEl: HTMLElement | null = null;

    try {
      const resp = await this.post(`${this.endpoint}/chat/stream`, body);

      if (!resp.ok || !resp.body) {
        this.typing.hidden = true;
        this.appendMessage('sys', 'Something went wrong. Please try again.');
        return;
      }

      const reader  = resp.body.getReader();
      const decoder = new TextDecoder();
      let buf = '';
      let done = false;

      while (!done) {
        const chunk = await reader.read();
        if (chunk.done) break;
        buf += decoder.decode(chunk.value, { stream: true });

        let nl: number;
        while ((nl = buf.indexOf('\n')) !== -1) {
          const line = buf.slice(0, nl);
          buf = buf.slice(nl + 1);
          if (!line.startsWith('data: ')) continue;
          const data = line.slice(6);

          if (data === '[DONE]') { done = true; break; }

          let evt: SseEvent;
          try { evt = JSON.parse(data) as SseEvent; } catch { continue; }

          this.typing.hidden = true;

          switch (evt.type) {
            case 'token':
              if (!botEl) botEl = this.appendMessage('bot', '');
              botEl.textContent += evt.value;
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
              this.appendMessage('sys', evt.reason);
              break;
          }
        }
      }
    } catch (err) {
      this.typing.hidden = true;
      if (!(err instanceof DOMException && err.name === 'AbortError'))
        this.appendMessage('sys', 'Connection error. Please try again.');
    } finally {
      this.typing.hidden    = true;
      this.streaming        = false;
      if (!this.awaitingEmail && !this.ticketCreated) {
        this.sendBtn.disabled = false;
        if (!this.panel.hidden) this.input.focus();
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

  private post(url: string, body: Record<string, unknown>): Promise<Response> {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (this.publicKey) headers['X-BotWire-Key'] = this.publicKey;
    return fetch(url, { method: 'POST', headers, body: JSON.stringify(body) });
  }

  private esc(s: string): string {
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  private q<T extends Element = HTMLElement>(sel: string): T {
    return this.shadow.querySelector<T>(sel)!;
  }
}

customElements.define('botwire-widget', BotWireWidget);
