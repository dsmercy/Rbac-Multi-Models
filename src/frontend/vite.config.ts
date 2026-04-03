import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      // Mirrors tsconfig.json paths — @/* maps to src/*
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    // Proxy API calls to the .NET backend during local dev so CORS is not needed
    proxy: {
      '/api': {
        target: 'https://localhost:51090',
        changeOrigin: true,
        secure: false,   // dev cert is self-signed
      },
      '/api/v1/hubs': {
        target: 'wss://localhost:51090',
        ws: true,
        changeOrigin: true,
        secure: false,
      },
    },
  },
  build: {
    sourcemap: true // ✅ IMPORTANT for debugging
  }
});
