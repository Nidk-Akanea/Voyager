import { defineConfig } from 'vite';

// Preload script: one CommonJS file, the only format a sandboxed preload can load.
export default defineConfig({
  build: {
    outDir: 'dist',
    emptyOutDir: false,
    target: 'chrome140',
    minify: false,
    sourcemap: true,
    lib: { entry: 'src/preload/preload.ts', formats: ['cjs'], fileName: () => 'preload.cjs' },
    rolldownOptions: { external: ['electron'] },
  },
});
