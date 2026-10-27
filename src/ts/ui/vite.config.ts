import { resolve } from 'node:path';

import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

import { servePluginFiles, sharedModules } from './vite.plugins';

// The development server port can be imposed by Aspire through PORT; 5173 otherwise.
const DEFAULT_DEV_PORT = 5173;
const CHUNK_SIZE_WARNING_LIMIT_KB = 1024;

// In development the plugins are the ones built in the repository (pwsh tools/build-plugins.ps1), or those at the
// address Aspire passes.
const PLUGINS_DIRECTORY =
  process.env.VOYAGER_PLUGINS_DIRECTORY ?? resolve(import.meta.dirname, '..', '..', '..', 'artifacts', 'plugins');

export default defineConfig({
  // Relative paths: the packaged desktop app serves the built files through its own protocol.
  base: './',
  plugins: [react(), sharedModules(), servePluginFiles(PLUGINS_DIRECTORY)],
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
