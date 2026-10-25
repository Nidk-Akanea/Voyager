import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// The development server port can be imposed by Aspire through PORT; 5173 otherwise.
const DEFAULT_DEV_PORT = 5173;
const CHUNK_SIZE_WARNING_LIMIT_KB = 1024;

export default defineConfig({
  // Relative paths: the packaged desktop app serves the built files through its own protocol.
  base: './',
  plugins: [react()],
  server: {
    port: Number(process.env.PORT ?? DEFAULT_DEV_PORT),
    strictPort: true,
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    // One bundle read from the local disk by the desktop app: its size matters less than on the web.
    chunkSizeWarningLimit: CHUNK_SIZE_WARNING_LIMIT_KB,
  },
});
