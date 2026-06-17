import { forwardRef, useEffect, useImperativeHandle, useRef, useState } from 'react';
import { BotWireClient, BotWireError } from 'botwire-js';

interface Message {
  role: 'user' | 'bot' | 'sys';
  text: string;
}

const GREETING: Message = {
  role: 'bot',
  text: 'Hi! Ask me anything about Acme Store — orders, shipping, returns.',
};

/** Imperative handle so the page's suggestion chips can drive the chat. */
export interface ChatHandle {
  ask: (question: string) => void;
}

// One client for the widget. The SDK owns the session token and self-heals a stale
// one — exactly what we exercise against Redis: after an API restart the session
// in Redis survives, so history is not lost. Reset drops the token for a fresh one.
const client = new BotWireClient({ endpoint: '/support' });

export const ChatWidget = forwardRef<ChatHandle>(function ChatWidget(_props, ref) {
  const [open, setOpen] = useState(false);
  const [messages, setMessages] = useState<Message[]>([GREETING]);
  const [input, setInput] = useState('');
  const [busy, setBusy] = useState(false);
  // When the bot asks for an email (escalation), show a contact field and resend
  // the pending question with the email attached.
  const [needContact, setNeedContact] = useState(false);
  const [pendingQuestion, setPendingQuestion] = useState('');
  const scrollRef = useRef<HTMLDivElement>(null);
  const abortRef = useRef<AbortController | null>(null);

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight });
  }, [messages, open]);

  useImperativeHandle(ref, () => ({
    ask(question: string) {
      setOpen(true);
      if (busy) return;
      setMessages((m) => [...m, { role: 'user', text: question }]);
      void send(question);
    },
  }));

  async function send(text: string, contactEmail?: string) {
    setBusy(true);
    const controller = new AbortController();
    abortRef.current = controller;
    // Start an empty bot message we stream tokens into.
    setMessages((m) => [...m, { role: 'bot', text: '' }]);

    try {
      for await (const evt of client.streamChat(text, { contactEmail, signal: controller.signal })) {
        switch (evt.type) {
          case 'delta':
            setMessages((m) => appendToLastBot(m, evt.delta));
            break;
          case 'collect_contact':
            setNeedContact(true);
            setPendingQuestion(text);
            setMessages((m) => replaceLastBotIfEmpty(m, 'Please share your email so our team can follow up.'));
            break;
          case 'escalated':
            setMessages((m) =>
              replaceLastBotIfEmpty(m, `${evt.message}${evt.ticketId ? ` (ticket ${evt.ticketId})` : ''}`));
            setNeedContact(false);
            setPendingQuestion('');
            break;
          case 'blocked':
            setMessages((m) => replaceLastBotIfEmpty(m, `Sorry — that message was blocked (${evt.reason}).`));
            break;
          case 'done':
            break;
        }
      }
    } catch (e) {
      if (controller.signal.aborted) return; // reset/close superseded this turn
      const msg = e instanceof BotWireError ? e.message : 'Something went wrong. Please try again.';
      setMessages((m) => replaceLastBotIfEmpty(m, msg));
    } finally {
      if (abortRef.current === controller) {
        abortRef.current = null;
        setBusy(false);
      }
    }
  }

  function reset() {
    // Abort any in-flight turn so it can't write into the fresh session, then drop
    // the session token — the SDK creates a new server session on the next send.
    abortRef.current?.abort();
    abortRef.current = null;
    client.setSessionToken(null);
    setMessages([GREETING]);
    setInput('');
    setBusy(false);
    setNeedContact(false);
    setPendingQuestion('');
  }

  function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    const text = input.trim();
    if (!text || busy) return;
    setInput('');
    setMessages((m) => [...m, { role: 'user', text }]);
    void send(text);
  }

  function onSubmitContact(e: React.FormEvent) {
    e.preventDefault();
    const email = input.trim();
    if (!email || busy) return;
    setInput('');
    setNeedContact(false);
    setMessages((m) => [...m, { role: 'user', text: email }]);
    void send(pendingQuestion, email);
  }

  return (
    <>
      <button className="chat-fab" onClick={() => setOpen((o) => !o)} aria-label="Open support chat">
        {open ? '×' : '💬'}
      </button>

      {open && (
        <div className="chat-panel">
          <div className="chat-head">
            <span>Acme Support</span>
            <div className="chat-head-actions">
              <button onClick={reset} title="New conversation" aria-label="Reset conversation">
                ↺
              </button>
              <button onClick={() => setOpen(false)} title="Close" aria-label="Close chat">
                ×
              </button>
            </div>
          </div>
          <div className="chat-log" ref={scrollRef}>
            {messages.map((m, i) => (
              <div key={i} className={`chat-msg chat-msg-${m.role}`}>
                {m.text || (m.role === 'bot' && busy ? '…' : '')}
              </div>
            ))}
          </div>
          <form className="chat-input" onSubmit={needContact ? onSubmitContact : onSubmit}>
            <input
              value={input}
              onChange={(e) => setInput(e.target.value)}
              placeholder={needContact ? 'your@email.com' : 'Type a message…'}
              type={needContact ? 'email' : 'text'}
              disabled={busy}
              autoFocus
            />
            <button type="submit" disabled={busy || !input.trim()}>
              Send
            </button>
          </form>
        </div>
      )}
    </>
  );
});

function appendToLastBot(messages: Message[], delta: string): Message[] {
  const copy = messages.slice();
  const last = copy[copy.length - 1];
  if (last && last.role === 'bot') copy[copy.length - 1] = { ...last, text: last.text + delta };
  return copy;
}

function replaceLastBotIfEmpty(messages: Message[], text: string): Message[] {
  const copy = messages.slice();
  const last = copy[copy.length - 1];
  if (last && last.role === 'bot' && last.text === '') copy[copy.length - 1] = { ...last, text };
  else copy.push({ role: 'bot', text });
  return copy;
}
