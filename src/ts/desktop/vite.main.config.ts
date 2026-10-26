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
    // velopack holds native code and electron-log needs the real Node.js modules: both ship as they
    // are, next to the app, not bundled (src/packaging/packageApp.ts).
    rolldownOptions: { external: ['electron', 'velopack', /^electron-log(\/|$)/, /^node:/] },
  },
});
