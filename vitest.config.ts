import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// Tests of every TypeScript package. Files testing React components declare
// `// @vitest-environment jsdom` themselves; the rest run in Node.js.
export default defineConfig({
  plugins: [react()],
  test: {
    include: ['src/ts/**/src/**/*.test.{ts,tsx}', 'plugins/*/ui/src/**/*.test.{ts,tsx}'],
    setupFiles: ['./vitest.setup.ts'],
    passWithNoTests: true,
  },
});
