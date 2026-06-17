import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The BotWire support endpoints (/support/*) and the sample product API (/api/*)
// are served by RedisShop.Api. Proxy them in dev so the browser sees a same-origin
// app and the BotWireClient can use relative paths.
const API_TARGET = process.env.SHOP_API_TARGET ?? 'http://localhost:5180';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/support': { target: API_TARGET, changeOrigin: true },
      '/api': { target: API_TARGET, changeOrigin: true },
    },
  },
});
