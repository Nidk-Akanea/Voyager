import { defineConfig } from 'vite';

// Main process: one ES module for Electron's Node.js runtime.
export default defineConfig({
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    target: 'node22',
    minify: false,
    sourcemap: true,
    lib: { entry: 'src/main/main.ts', formats: ['es'], fileName: () => 'main.js' },
    rolldownOptions: { external: ['electron', /^node:/] },
  },
});
