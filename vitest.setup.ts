import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

// jsdom has no ResizeObserver, which some Fluent UI components (MessageBar) need to lay themselves out.
const environment: Partial<Pick<typeof globalThis, 'ResizeObserver'>> = globalThis;
environment.ResizeObserver ??= class {
  observe(): void {
    // Nothing to observe: jsdom does not lay out the page.
  }
  unobserve(): void {
    // Nothing to stop observing.
  }
  disconnect(): void {
    // Nothing to disconnect.
  }
};

// Unmounts what each React test rendered, so tests do not see each other's screens.
afterEach(() => {
  cleanup();
});
