// @vitest-environment jsdom
import { screen } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';

import { PluginStatus } from '@voyager/grpc-client';

import { plugin, renderPage } from '../testing/pluginManagementTesting';

afterEach(() => {
  sessionStorage.clear();
});

describe('AppShell', () => {
  it('opens on "About", and goes to "Plugins" with its tab', async () => {
    renderPage({ plugins: [plugin('diagnostics-clock')], shell: true });

    expect(await screen.findByRole('heading', { name: 'À propos de Voyager' })).toBeTruthy();
    await userEvent.click(screen.getByRole('tab', { name: 'Plugins' }));

    expect(await screen.findByRole('heading', { name: 'Plugins', level: 1 })).toBeTruthy();
    expect(await screen.findByRole('button', { name: 'Désactiver' })).toBeTruthy();
  });

  it('opens again on the tab chosen before a reload', async () => {
    renderPage({ plugins: [plugin('diagnostics-clock')], shell: true });
    await userEvent.click(await screen.findByRole('tab', { name: 'Plugins' }));

    expect(sessionStorage.getItem('voyager.screen')).toBe('plugins');
  });

  it('opens on "Plugins" when it was the tab of the window before its reload', async () => {
    sessionStorage.setItem('voyager.screen', 'plugins');

    renderPage({ plugins: [plugin('diagnostics-clock')], shell: true });

    expect(await screen.findByRole('heading', { name: 'Plugins', level: 1 })).toBeTruthy();
    expect(screen.queryByRole('heading', { name: 'À propos de Voyager' })).toBeNull();
  });

  it('says on "About" that no plugin is active when they are all disabled', async () => {
    renderPage({ plugins: [plugin('diagnostics-clock', { status: PluginStatus.DISABLED })], shell: true });

    expect(await screen.findByText('Aucun plugin n’est actif.')).toBeTruthy();
  });
});
