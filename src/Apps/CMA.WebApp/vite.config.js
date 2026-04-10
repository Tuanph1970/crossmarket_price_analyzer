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
        target: 'http://product-api:8080',
        changeOrigin: true,
      },
      '/api/scores': {
        target: 'http://scoring-api:8080',
        changeOrigin: true,
      },
      '/api/matches': {
        target: 'http://matching-api:8080',
        changeOrigin: true,
      },
    },
  },
});
