import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The host platform is a regular Vite/React SPA.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 8080,
    strictPort: true,
  },
  build: {
    outDir: 'dist',
  },
});
