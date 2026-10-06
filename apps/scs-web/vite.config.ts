import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The SCS UI is distributed as ONE self-contained ESM file (scs-web.js).
// React is bundled inside: the host platform never shares a framework with us,
// which is what makes the module transparently embeddable anywhere.
export default defineConfig({
  plugins: [react()],
  define: {
    // Lib builds don't get Vite's default NODE_ENV replacement.
    'process.env.NODE_ENV': JSON.stringify('production'),
  },
  build: {
    lib: {
      entry: 'src/main.tsx',
      formats: ['es'],
      fileName: () => 'scs-web.js',
    },
    minify: true,
  },
  server: {
    port: 5174,
  },
});
