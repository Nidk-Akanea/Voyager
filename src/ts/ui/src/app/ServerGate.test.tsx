// @vitest-environment jsdom
import { act, cleanup, render, screen } from '@testing-library/react';
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

import { QueryClient } from '@tanstack/react-query';

import { useAppContext } from './appContext';
import { AppProviders } from './AppProviders';
import type { ServerBridge } from './bridge';
import { ServerConnection } from './ServerConnection';
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
  return { getServers: () => Promise.resolve(servers), getAppVersion: () => Promise.resolve('0.4.0') };
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
      // As the server sends it: a google.rpc.ErrorInfo, reason in field 1, domain in field 2.
      const encodedReason = new TextEncoder().encode(reason);
      const encodedDomain = new TextEncoder().encode('voyager');
      error.details.push({
        type: 'google.rpc.ErrorInfo',
        value: new Uint8Array([
          0x0a,
          encodedReason.length,
          ...encodedReason,
          0x12,
          encodedDomain.length,
          ...encodedDomain,
        ]),
      });
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
    // Twice: the check tries once more before it counts the server as unreachable.
    renderGate([SERVER], fakeClients(failure(Code.Unavailable), failure(Code.Unavailable), answers));

    // After the second try, a second later.
    expect(await screen.findByText(/Le serveur ne répond pas/, undefined, { timeout: 4000 })).toBeTruthy();
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

  it('keeps the screens, with the problem above them, when a server that answered stops answering', async () => {
    const clients = fakeClients(answers);
    const queryClient = new QueryClient();
    render(
      <ShellProviders locale="fr">
        <AppProviders locale="fr" server={SERVER} clients={clients} queryClient={queryClient}>
          <ServerConnection>
            <Screens />
          </ServerConnection>
        </AppProviders>
      </ShellProviders>,
    );
    expect(await screen.findByText('Screens of http://localhost:5180')).toBeTruthy();

    // What the check every half minute finds when the server stops answering.
    vi.mocked(clients.diagnostics.getServiceInfo).mockImplementation(failure(Code.Unavailable));
    await act(() => queryClient.refetchQueries());

    expect(await screen.findByText(/Le serveur ne répond pas/, undefined, { timeout: 4000 })).toBeTruthy();
    expect(screen.getByText('Screens of http://localhost:5180')).toBeTruthy();
  });

  it('counts an error of the server itself as a refusal, not as a server that does not answer', async () => {
    renderGate([SERVER], fakeClients(failure(Code.Unknown)));

    expect(await screen.findByText(/a rencontré une erreur/)).toBeTruthy();
  });

  it('tells any other refusal apart from an app too old', async () => {
    renderGate([SERVER], fakeClients(failure(Code.FailedPrecondition, 'SOMETHING_ELSE')));

    expect(await screen.findByText(/Le serveur a refusé la demande/)).toBeTruthy();
  });
});
