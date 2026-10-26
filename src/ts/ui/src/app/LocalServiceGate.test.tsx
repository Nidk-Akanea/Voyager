// @vitest-environment jsdom
import { act, render, screen } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { LocalServiceState } from '@voyager/grpc-client';

import type { Locale } from '../i18n/localization';
import type { VoyagerBridge } from './bridge';
import { LocalServiceGate } from './LocalServiceGate';
import { ShellProviders } from './ShellProviders';
import { useAppContext } from './appContext';

function readyAt(port: number): LocalServiceState {
  return { status: 'ready', connection: { url: `http://127.0.0.1:${String(port)}`, token: `token-${String(port)}` } };
}

/** A bridge the test drives by hand, the way the desktop app pushes changes. */
function fakeBridge(initial: LocalServiceState) {
  let listener: ((state: LocalServiceState) => void) | undefined;
  const bridge: VoyagerBridge = {
    getLocalServiceState: () => Promise.resolve(initial),
    onLocalServiceStateChange: (given) => {
      listener = given;
      return () => {
        listener = undefined;
      };
    },
    retryLocalService: vi.fn(() => Promise.resolve()),
  };
  return {
    bridge,
    isListening: () => listener !== undefined,
    push: (state: LocalServiceState) => {
      act(() => listener?.(state));
    },
  };
}

function AddressOfService() {
  return <p>{useAppContext().locale}</p>;
}

function renderGate(bridge: VoyagerBridge, locale: Locale = 'fr') {
  return render(
    <ShellProviders locale={locale}>
      <LocalServiceGate bridge={bridge} locale={locale}>
        <AddressOfService />
      </LocalServiceGate>
    </ShellProviders>,
  );
}

describe('LocalServiceGate', () => {
  it('says the app is starting until the service is ready, then shows the app', async () => {
    const { bridge, push } = fakeBridge({ status: 'starting' });
    renderGate(bridge);

    expect(await screen.findByText('Démarrage de Voyager…')).toBeTruthy();
    push(readyAt(4321));

    expect(await screen.findByText('fr')).toBeTruthy();
    expect(screen.queryByText('Démarrage de Voyager…')).toBeNull();
  });

  it('says so when the service restarts, then shows the app again', async () => {
    const { bridge, push } = fakeBridge(readyAt(4321));
    renderGate(bridge);
    expect(await screen.findByText('fr')).toBeTruthy();

    push({ status: 'restarting' });
    expect(await screen.findByText('Le service local de Voyager s’est arrêté, redémarrage…')).toBeTruthy();
    expect(screen.queryByText('fr')).toBeNull();

    push(readyAt(4322));
    expect(await screen.findByText('fr')).toBeTruthy();
  });

  it('offers to try again when the service failed', async () => {
    const { bridge, push } = fakeBridge({ status: 'failed' });
    renderGate(bridge);

    expect(await screen.findByText('Le service local de Voyager s’est arrêté et n’a pas pu redémarrer.')).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Réessayer' }));
    expect(bridge.retryLocalService).toHaveBeenCalledOnce();

    push({ status: 'starting' });
    expect(await screen.findByText('Démarrage de Voyager…')).toBeTruthy();
  });

  it('stops listening once removed from the page', () => {
    const { bridge, isListening } = fakeBridge({ status: 'starting' });
    const { unmount } = renderGate(bridge);
    expect(isListening()).toBe(true);

    unmount();

    expect(isListening()).toBe(false);
  });

  it('keeps going when asking to try again fails', async () => {
    const { bridge } = fakeBridge({ status: 'failed' });
    vi.mocked(bridge.retryLocalService).mockRejectedValueOnce(new Error('refused'));
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    renderGate(bridge);

    await userEvent.click(await screen.findByRole('button', { name: 'Réessayer' }));

    await vi.waitFor(() => {
      expect(consoleError).toHaveBeenCalled();
    });
    expect(screen.getByRole('button', { name: 'Réessayer' })).toBeTruthy();
    consoleError.mockRestore();
  });

  it('speaks English when English is chosen', async () => {
    const { bridge } = fakeBridge({ status: 'failed' });
    renderGate(bridge, 'en');

    expect(await screen.findByRole('button', { name: 'Try again' })).toBeTruthy();
  });

  it('prefers a change pushed before the first answer', async () => {
    let answer: (state: LocalServiceState) => void = () => undefined;
    let push: (state: LocalServiceState) => void = () => undefined;
    const bridge: VoyagerBridge = {
      getLocalServiceState: () => new Promise((resolve) => (answer = resolve)),
      onLocalServiceStateChange: (listener) => {
        push = listener;
        return () => undefined;
      },
      retryLocalService: () => Promise.resolve(),
    };
    renderGate(bridge);

    act(() => {
      push(readyAt(4322));
    });
    await act(async () => {
      answer({ status: 'starting' });
      await Promise.resolve();
    });

    expect(await screen.findByText('fr')).toBeTruthy();
  });
});
