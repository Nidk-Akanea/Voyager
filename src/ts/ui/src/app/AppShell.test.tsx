// @vitest-environment jsdom
import { QueryClient } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { PluginOrigin, PluginRejectionReason, PluginStatus, type VoyagerClients } from '@voyager/grpc-client';

import type { PluginRuntime } from '../plugins/pluginRuntime';
import { AppProviders } from './AppProviders';
import { AppShell } from './AppShell';
import { ShellProviders } from './ShellProviders';

const RUNTIME: PluginRuntime = {
  importModule: () => Promise.reject(new Error('not in these tests')),
  fetchText: () => Promise.resolve(undefined),
  resolve: (path) => `test://${path}`,
};

function plugin(id: string, status: PluginStatus) {
  return {
    folder: id,
    id,
    version: '1.2.3',
    status,
    rejectionReason: PluginRejectionReason.UNSPECIFIED,
    uiModule: '',
    provides: [],
    contracts: [],
    origin: PluginOrigin.SHIPPED,
  };
}

function renderShell(plugins: readonly unknown[]) {
  const clients = {
    diagnostics: { getServiceInfo: () => new Promise(() => undefined) },
    pluginHost: {
      listPlugins: () => Promise.resolve({ plugins }),
      getDiagnosticFacts: () => Promise.resolve({ sources: [] }),
    },
  } as unknown as VoyagerClients;
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <ShellProviders locale="fr">
      <AppProviders locale="fr" clients={clients} queryClient={queryClient} pluginRuntime={RUNTIME}>
        <AppShell />
      </AppProviders>
    </ShellProviders>,
  );
}

describe('AppShell', () => {
  it('opens on "About", the only tab', async () => {
    renderShell([plugin('diagnostics-clock', PluginStatus.LOADED)]);

    expect(await screen.findByRole('heading', { name: 'À propos de Voyager' })).toBeTruthy();
    expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['À propos']);
  });

  it('says on "About" that no plugin is active when they are all disabled', async () => {
    renderShell([plugin('diagnostics-clock', PluginStatus.DISABLED)]);

    expect(await screen.findByText('Aucun plugin n’est actif.')).toBeTruthy();
  });
});
