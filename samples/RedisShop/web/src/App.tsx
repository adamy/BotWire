import { useEffect, useRef, useState } from 'react';
import type { Product } from './types.ts';
import { ProductCard } from './ProductCard.tsx';
import { ChatWidget, type ChatHandle } from './ChatWidget.tsx';
import { SUGGESTIONS } from './suggestions.ts';

export function App() {
  const [products, setProducts] = useState<Product[]>([]);
  const [error, setError] = useState<string | null>(null);
  const chatRef = useRef<ChatHandle>(null);

  useEffect(() => {
    fetch('/api/products')
      .then((r) => {
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        return r.json() as Promise<Product[]>;
      })
      .then(setProducts)
      .catch((e) => setError(String(e)));
  }, []);

  return (
    <div className="page">
      <header className="header">
        <h1>Acme Store</h1>
        <p>Everyday essentials — bags, drinkware, apparel &amp; more.</p>
      </header>

      <main>
        {error && <p className="error">Failed to load products: {error}</p>}
        <div className="grid">
          {products.map((p) => (
            <ProductCard key={p.id} product={p} />
          ))}
        </div>

        {/* Q&A hints — click a question to send it straight to the support bot. */}
        <section className="try-section">
          <h2>Try asking the assistant</h2>
          <p className="try-sub">Click any question below to send it directly to the chat.</p>
          <div className="topics">
            {SUGGESTIONS.map((group) => (
              <div className="topic" key={group.heading}>
                <h4>{group.heading}</h4>
                <ul>
                  {group.questions.map((q) => (
                    <li key={q} onClick={() => chatRef.current?.ask(q)}>
                      {q}
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        </section>
      </main>

      {/* Custom chatbox built on the botwire-js SDK (headless BotWireClient). */}
      <ChatWidget ref={chatRef} />
    </div>
  );
}
