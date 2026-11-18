/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

import screens from './splash-screens.json' with { type: 'json' };
import { serviceWorker } from './vite-plugins/service-worker.ts';
import { splashScreens } from './vite-plugins/splash-screens.ts';

// The server that "npm run dev" forwards API requests to. Start it with "dotnet run".
const SERVER = 'http://localhost:8080';

export default defineConfig({
  plugins: [react(), splashScreens(screens), serviceWorker('src/service-worker.js')],
  server: {
    proxy: Object.fromEntries(
      ['/api', '/sign-in', '/sign-out', '/setup', '/healthz', '/auth.css', '^/mcp$'].map((path) => [
        path,
        SERVER,
      ]),
    ),
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['src/test/setup.ts'],
    css: false,
  },
});
