/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';

// Target del proxy configurable por entorno (Docker/compose apunta al servicio "api");
// fallback al puerto de dev en host.
const apiTarget = process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5245';
const allowedHost = process.env.VITE_ALLOWED_HOST;

export function crearHostsPermitidos(host: string | undefined): string[] {
  return host ? [host] : [];
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      strategies: 'injectManifest',
      srcDir: 'src/pwa',
      filename: 'service-worker.ts',
      injectManifest: {
        globPatterns: ['**/*.{js,css,html,ico,png,svg,woff2}'],
      },
      manifest: {
        name: 'Caserito',
        short_name: 'Caserito',
        description: 'Compra, vende e intercambia cerca de ti',
        lang: 'es-BO',
        theme_color: '#165C3B',
        background_color: '#F8F6F1',
        display: 'standalone',
        start_url: '/',
        icons: [
          { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
          { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png' },
          {
            src: 'pwa-maskable-192x192.png',
            sizes: '192x192',
            type: 'image/png',
            purpose: 'maskable',
          },
          {
            src: 'pwa-maskable-512x512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'maskable',
          },
        ],
      },
    }),
  ],
  server: {
    host: true,
    allowedHosts: crearHostsPermitidos(allowedHost),
    proxy: {
      '/health': apiTarget,
      '/api': apiTarget,
      '/hubs': {
        target: apiTarget,
        ws: true,
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    maxWorkers: 2,
  },
});
