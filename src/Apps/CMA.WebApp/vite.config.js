import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 3004,
    proxy: {
      '/api/products': {
        target: 'http://localhost:5001',
        changeOrigin: true,
      },
      '/api/scores': {
        target: 'http://localhost:5003',
        changeOrigin: true,
      },
      '/api/matches': {
        target: 'http://localhost:5002',
        changeOrigin: true,
      },
      '/api/auth': {
        target: 'http://localhost:5005',
        changeOrigin: true,
      },
      '/api/watchlist': {
        target: 'http://localhost:5005',
        changeOrigin: true,
      },
      '/api/alerts': {
        target: 'http://localhost:5005',
        changeOrigin: true,
      },
    },
  },
});
