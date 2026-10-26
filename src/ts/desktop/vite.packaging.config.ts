import { defineConfig } from 'vite';

// Packaging script (src/packaging/packageApp.ts): run by Node.js on the machine that builds the app,
// never shipped with it.
export default defineConfig({
  build: {
    outDir: 'dist-tools',
    emptyOutDir: true,
    target: 'node22',
    minify: false,
    lib: { entry: 'src/packaging/packageApp.ts', formats: ['es'], fileName: () => 'packageApp.mjs' },
    rolldownOptions: { external: ['@electron/fuses', '@electron/packager', /^node:/] },
  },
});
