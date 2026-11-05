// @vitest-environment jsdom
import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { PluginStatus } from '@voyager/grpc-client';

import { plugin, renderPage } from '../testing/pluginManagementTesting';

describe('AppShell', () => {
  it('opens on "About", without the "Plugins" tab of the management on this machine', async () => {
    renderPage({ plugins: [plugin('diagnostics-clock')], shell: true });

    expect(await screen.findByRole('heading', { name: 'À propos de Voyager' })).toBeTruthy();
    expect(screen.getByRole('tab', { name: 'À propos' })).toBeTruthy();
    expect(screen.queryByRole('tab', { name: 'Plugins' })).toBeNull();
  });

  it('says on "About" that no plugin is active when they are all disabled', async () => {
    renderPage({ plugins: [plugin('diagnostics-clock', { status: PluginStatus.DISABLED })], shell: true });

    expect(await screen.findByText('Aucun plugin n’est actif.')).toBeTruthy();
  });
});
