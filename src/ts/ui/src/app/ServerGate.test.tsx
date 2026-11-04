// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

import {
  APP_VERSION_TOO_OLD_REASON,
  Code,
  ConnectError,
  type GetServiceInfoResponse,
  type RegisteredServer,
  type VoyagerClients,
} from '@voyager/grpc-client';

import { useAppContext } from './appContext';
import type { ServerBridge } from './bridge';
import { ServerGate } from './ServerGate';
import { ShellProviders } from './ShellProviders';

// The message bars measure their actions; jsdom has no ResizeObserver.
beforeAll(() => {
  vi.stubGlobal(
    'ResizeObserver',
    class {
      observe = vi.fn();
      unobserve = vi.fn();
      disconnect = vi.fn();
    },
  );
});

afterEach(cleanup);

const SERVER: RegisteredServer = { id: 'development', address: 'http://localhost:5180' };

/** Clients whose "About" call answers what each test says, one answer after the other. */
function fakeClients(...answers: (() => Promise<unknown>)[]): VoyagerClients {
  const getServiceInfo = vi.fn(() => (answers.length > 1 ? answers.shift() : answers[0])?.() ?? Promise.resolve({}));
  return { diagnostics: { getServiceInfo }, pluginHost: {} } as unknown as VoyagerClients;
}

function bridgeWith(servers: readonly RegisteredServer[]): ServerBridge {
  return { getServers: () => Promise.resolve(servers) };
}

/** What the screens see once the server answers: the server they show. */
function Screens() {
  const { server } = useAppContext();
  return <p>Screens of {server?.address}</p>;
}

function renderGate(servers: readonly RegisteredServer[], clients: VoyagerClients) {
  render(
    <ShellProviders locale="fr">
      <ServerGate bridge={bridgeWith(servers)} locale="fr" createClients={() => clients}>
        <Screens />
      </ServerGate>
    </ShellProviders>,
  );
}

function failure(code: Code, reason?: string): () => Promise<never> {
  return () => {
    const error = new ConnectError('refused', code);
    if (reason !== undefined) {
      // As the server sends it: a google.rpc.ErrorInfo whose reason is field 1.
      const encoded = new TextEncoder().encode(reason);
      error.details.push({ type: 'google.rpc.ErrorInfo', value: new Uint8Array([0x0a, encoded.length, ...encoded]) });
    }
    return Promise.reject(error);
  };
}

const answers = (): Promise<GetServiceInfoResponse> => Promise.resolve({ version: '0.1.0' } as GetServiceInfoResponse);

describe('ServerGate', () => {
  it('says that no server is configured when the app knows none', async () => {
    renderGate([], fakeClients(answers));

    expect(await screen.findByText(/Aucun serveur n’est configuré/)).toBeTruthy();
  });

  it('shows the screens of the server once it answers', async () => {
    renderGate([SERVER], fakeClients(answers));

    expect(await screen.findByText('Screens of http://localhost:5180')).toBeTruthy();
  });

  it('says the server does not answer, and shows the screens once it answers again', async () => {
    renderGate([SERVER], fakeClients(failure(Code.Unavailable), answers));

    expect(await screen.findByText(/Le serveur ne répond pas/)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Réessayer' }));

    expect(await screen.findByText('Screens of http://localhost:5180')).toBeTruthy();
  });

  it('asks to update Voyager when the server no longer serves this version, without "Try again"', async () => {
    renderGate([SERVER], fakeClients(failure(Code.FailedPrecondition, APP_VERSION_TOO_OLD_REASON)));

    expect(await screen.findByText(/trop ancienne pour ce serveur/)).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Réessayer' })).toBeNull();
  });

  it('says the server asks to sign in', async () => {
    renderGate([SERVER], fakeClients(failure(Code.Unauthenticated)));

    expect(await screen.findByText(/demande de vous connecter/)).toBeTruthy();
  });

  it('tells any other refusal apart from an app too old', async () => {
    renderGate([SERVER], fakeClients(failure(Code.FailedPrecondition, 'SOMETHING_ELSE')));

    expect(await screen.findByText(/Le serveur a refusé la demande/)).toBeTruthy();
  });
});
