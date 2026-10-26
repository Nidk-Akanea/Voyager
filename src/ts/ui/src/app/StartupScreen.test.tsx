// @vitest-environment jsdom
import { act, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { StartupState } from '@voyager/grpc-client';

import type { Locale } from '../i18n/localization';
import type { StartupBridge } from './bridge';
import { ShellProviders } from './ShellProviders';
import { StartupScreen } from './StartupScreen';

/** A bridge the test drives by hand, the way the desktop app pushes changes. */
function fakeBridge(initial: StartupState) {
  let listener: ((state: StartupState) => void) | undefined;
  const bridge: StartupBridge = {
    getStartupState: () => Promise.resolve(initial),
    onStartupStateChange: (given) => {
      listener = given;
      return () => {
        listener = undefined;
      };
    },
  };
  return {
    bridge,
    isListening: () => listener !== undefined,
    push: (state: StartupState) => {
      act(() => listener?.(state));
    },
  };
}

function renderScreen(bridge: StartupBridge, locale: Locale = 'fr') {
  return render(
    <ShellProviders locale={locale}>
      <StartupScreen bridge={bridge} />
    </ShellProviders>,
  );
}

describe('StartupScreen', () => {
  it('says it looks for updates, then follows the download of one with its progress', async () => {
    const { bridge, push } = fakeBridge({ status: 'checking-updates' });
    renderScreen(bridge);

    expect(await screen.findByText('Recherche de mises à jour…')).toBeTruthy();

    push({ status: 'downloading-update', version: '0.3.0', percent: 42 });
    expect(await screen.findByText(/Téléchargement de la version\s+.?0\.3\.0.?…/)).toBeTruthy();
    expect(screen.getByText(/42\W*%/)).toBeTruthy();
    expect(screen.getByRole('progressbar').getAttribute('aria-valuenow')).toBe('42');
  });

  it('says the app restarts when it installs an update', async () => {
    const { bridge } = fakeBridge({ status: 'installing-update', version: '0.3.0' });
    renderScreen(bridge);

    expect(await screen.findByText(/Voyager va redémarrer/)).toBeTruthy();
  });

  it('says the app starts once the update step is over', async () => {
    const { bridge } = fakeBridge({ status: 'starting-service' });
    renderScreen(bridge);

    expect(await screen.findByText('Démarrage de Voyager…')).toBeTruthy();
  });

  it('speaks English when English is chosen', async () => {
    const { bridge } = fakeBridge({ status: 'checking-updates' });
    renderScreen(bridge, 'en');

    expect(await screen.findByText('Looking for updates…')).toBeTruthy();
  });

  it('stops listening once removed from the page', () => {
    const { bridge, isListening } = fakeBridge({ status: 'checking-updates' });
    const { unmount } = renderScreen(bridge);
    expect(isListening()).toBe(true);

    unmount();

    expect(isListening()).toBe(false);
  });

  it('says the app starts in English too', async () => {
    const { bridge } = fakeBridge({ status: 'starting-service' });
    renderScreen(bridge, 'en');

    expect(await screen.findByText('Starting Voyager…')).toBeTruthy();
  });

  it('prefers a change pushed before the first answer', async () => {
    let answer: (state: StartupState) => void = () => undefined;
    let push: (state: StartupState) => void = () => undefined;
    const bridge: StartupBridge = {
      getStartupState: () => new Promise((resolve) => (answer = resolve)),
      onStartupStateChange: (listener) => {
        push = listener;
        return () => undefined;
      },
    };
    renderScreen(bridge);

    act(() => {
      push({ status: 'starting-service' });
    });
    await act(async () => {
      answer({ status: 'checking-updates' });
      await Promise.resolve();
    });

    expect(await screen.findByText('Démarrage de Voyager…')).toBeTruthy();
  });

  it('keeps saying the app starts when the state cannot be read', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const bridge: StartupBridge = {
      getStartupState: () => Promise.reject(new Error('refused')),
      onStartupStateChange: () => () => undefined,
    };
    renderScreen(bridge);

    await vi.waitFor(() => {
      expect(consoleError).toHaveBeenCalled();
    });
    expect(screen.getByText('Recherche de mises à jour…')).toBeTruthy();
    consoleError.mockRestore();
  });
});
