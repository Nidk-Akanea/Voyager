import { defineConfig } from 'vite';

// What a plugin may import is provided by the page, through its import map (ADR-0015): none of it is bundled here,
// so that the plugin and the page use the same React.
const SHARED_BY_THE_PAGE = [
  'react',
  'react/jsx-runtime',
  'react-dom',
  'react-dom/client',
  '@fluentui/react-components',
  '@fluent/react',
  '@voyager/plugin-sdk',
];

export default defineConfig({
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    lib: { entry: 'src/index.ts', formats: ['es'], fileName: 'index' },
    rollupOptions: { external: SHARED_BY_THE_PAGE },
  },
});
