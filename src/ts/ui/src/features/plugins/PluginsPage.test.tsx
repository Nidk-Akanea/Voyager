// @vitest-environment jsdom
import { screen, waitFor, within } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import {
  ContractRole,
  ContractUseState,
  PluginRejectionReason,
  PluginRequestAction,
  PluginRequestFailure,
  PluginStatus,
} from '@voyager/grpc-client';

import {
  byText,
  cardAround,
  cardOf,
  createBridge,
  installed,
  PACKAGE,
  plugin,
  renderPage,
  SHA256,
  startingWith,
} from '../../testing/pluginManagementTesting';

describe('PluginsPage', () => {
  it('shows each shipped plugin with its version, origin, verification and state, without "Uninstall"', async () => {
    renderPage({ plugins: [plugin('diagnostics-runtime'), plugin('diagnostics-clock')] });

    const card = within(await cardOf('diagnostics-clock'));

    expect(card.getByText(byText('Version 1.2.3'))).toBeTruthy();
    expect(card.getByText('Livré avec Voyager')).toBeTruthy();
    expect(card.getByText('Garanti par l’application')).toBeTruthy();
    expect(card.getByText('Chargé')).toBeTruthy();
    expect(card.getByRole('button', { name: 'Désactiver' })).toBeTruthy();
    expect(card.queryByRole('button', { name: 'Désinstaller' })).toBeNull();
    expect(await cardOf('diagnostics-runtime')).toBeTruthy();
  });

  it('shows why a plugin was set aside, in the language of the application', async () => {
    renderPage({
      plugins: [
        plugin('old-tool', {
          status: PluginStatus.REJECTED,
          rejectionReason: PluginRejectionReason.CONTRACT_NOT_SUPPORTED,
          contracts: [
            { contract: 'old-source', version: 1, role: ContractRole.PROVIDED, state: ContractUseState.OBSOLETE },
          ],
        }),
      ],
    });

    const card = within(await cardOf('old-tool'));

    expect(card.getByText('Écarté')).toBeTruthy();
    expect(
      card.getByText('Il utilise un contrat dans une version que cette application ne prend pas en charge.'),
    ).toBeTruthy();
    expect(
      card.getByText(byText('old-source, version 1 : version retirée de cette application. Mettez à jour ce plugin.')),
    ).toBeTruthy();
  });

  it('shows what an installed plugin brings, when it was installed, and the name and fingerprint of its package', async () => {
    renderPage({ plugins: [installed('tool', { hasUi: true })] });

    const card = within(await cardOf('tool'));

    expect(card.getByText('Installé par vous')).toBeTruthy();
    expect(card.getByText('Non vérifié')).toBeTruthy();
    expect(card.getByText(byText('Apporte : du code .NET et une interface'))).toBeTruthy();
    expect(card.getByText(byText('Requiert plugin, version 1 : pris en charge'))).toBeTruthy();
    expect(card.getByText(startingWith('Installé le 4 mai 2026'))).toBeTruthy();
    expect(card.getByText(byText('Paquet d’origine : tool-1.2.3.voyagerplugin'))).toBeTruthy();
    expect(card.getByText(byText(`Empreinte (SHA-256) : ${SHA256}`))).toBeTruthy();
    expect(card.getByRole('button', { name: 'Désinstaller' })).toBeTruthy();
  });

  it('disables a plugin through the desktop app, says the change waits for a restart, and reads the lists again', async () => {
    const { bridge, listPlugins } = renderPage({ plugins: [plugin('diagnostics-clock')] });
    const card = within(await cardOf('diagnostics-clock'));

    await userEvent.click(card.getByRole('button', { name: 'Désactiver' }));

    expect(bridge.disable).toHaveBeenCalledWith({ origin: 'shipped', pluginId: 'diagnostics-clock' });
    expect(await screen.findByText('C’est noté : le changement sera appliqué au prochain redémarrage.')).toBeTruthy();
    await waitFor(() => {
      expect(listPlugins).toHaveBeenCalledTimes(2);
    });
    expect(bridge.listRequests).toHaveBeenCalledTimes(2);
  });

  it('enables a disabled plugin through the desktop app, which asks the user', async () => {
    const { bridge } = renderPage({ plugins: [plugin('diagnostics-clock', { status: PluginStatus.DISABLED })] });
    const card = within(await cardOf('diagnostics-clock'));

    expect(card.getByText('Désactivé')).toBeTruthy();
    await userEvent.click(card.getByRole('button', { name: 'Activer' }));

    expect(bridge.enable).toHaveBeenCalledWith({ origin: 'shipped', pluginId: 'diagnostics-clock' });
  });

  it('says nothing when the user declined the confirmation', async () => {
    const bridge = createBridge({ uninstall: vi.fn(() => Promise.resolve({ outcome: 'cancelled' as const })) });
    renderPage({ plugins: [installed('tool')], bridge });
    const card = within(await cardOf('tool'));

    await userEvent.click(card.getByRole('button', { name: 'Désinstaller' }));

    await waitFor(() => {
      expect(bridge.uninstall).toHaveBeenCalledWith({ origin: 'installed', pluginId: 'tool' });
    });
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.queryByText(/C’est noté/)).toBeNull();
  });

  it('shows a request that waits, and cancels it', async () => {
    const bridge = createBridge({
      listRequests: vi.fn(() =>
        Promise.resolve([{ action: 'disable', origin: 'shipped', pluginId: 'diagnostics-clock' }] as const),
      ),
    });
    renderPage({ plugins: [plugin('diagnostics-clock')], bridge });
    const card = within(await cardOf('diagnostics-clock'));

    expect(await card.findByText('En attente de redémarrage')).toBeTruthy();
    expect(
      card.getByText('Sera désactivé au prochain redémarrage. D’ici là, il continue de fonctionner.'),
    ).toBeTruthy();
    await userEvent.click(card.getByRole('button', { name: 'Annuler la demande' }));

    expect(bridge.cancelRequest).toHaveBeenCalledWith({ origin: 'shipped', pluginId: 'diagnostics-clock' });
    expect(await screen.findByText('Rien à appliquer : la demande en attente est annulée.')).toBeTruthy();
  });

  it('shows the installation of a new plugin before the restart, and cancels it', async () => {
    const bridge = createBridge({
      listRequests: vi.fn(() =>
        Promise.resolve([
          { action: 'install', origin: 'installed', pluginId: 'tool', packageName: 'tool-1.0.0.voyagerplugin' },
        ] as const),
      ),
    });
    renderPage({ plugins: [plugin('diagnostics-clock')], bridge });

    expect(await screen.findByText(byText('Nouveau plugin « tool »'))).toBeTruthy();
    expect(screen.getByText(byText('Paquet : tool-1.0.0.voyagerplugin'))).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Annuler la demande' }));

    expect(bridge.cancelRequest).toHaveBeenCalledWith({ origin: 'installed', pluginId: 'tool' });
  });

  it('says how many requests wait, and restarts to apply them', async () => {
    const bridge = createBridge({
      listRequests: vi.fn(() =>
        Promise.resolve([
          { action: 'disable', origin: 'shipped', pluginId: 'diagnostics-clock' },
          { action: 'uninstall', origin: 'installed', pluginId: 'tool' },
        ] as const),
      ),
      restart: vi.fn(() => new Promise<never>(() => undefined)),
    });
    renderPage({ plugins: [plugin('diagnostics-clock'), installed('tool')], bridge });

    expect(await screen.findByText(byText('2 demandes attendent le redémarrage.'))).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Redémarrer pour appliquer' }));

    expect(bridge.restart).toHaveBeenCalledTimes(1);
    expect(await screen.findByText('Redémarrage du service…')).toBeTruthy();
  });

  it('says where to restart a service the app does not run', async () => {
    const bridge = createBridge({
      listRequests: vi.fn(() =>
        Promise.resolve([{ action: 'disable', origin: 'shipped', pluginId: 'diagnostics-clock' }] as const),
      ),
      restart: vi.fn(() => Promise.resolve({ outcome: 'restart-elsewhere' as const })),
    });
    renderPage({ plugins: [plugin('diagnostics-clock')], bridge });

    await userEvent.click(await screen.findByRole('button', { name: 'Redémarrer pour appliquer' }));

    expect(await screen.findByText(/Le service est lancé par un autre programme/)).toBeTruthy();
  });

  it('shows no banner when no request waits', async () => {
    renderPage({ plugins: [plugin('diagnostics-clock')] });

    await cardOf('diagnostics-clock');

    expect(screen.queryByRole('button', { name: 'Redémarrer pour appliquer' })).toBeNull();
  });

  it('shows a request that failed, with its reason', async () => {
    renderPage({
      plugins: [
        installed('tool', {
          failedRequest: PluginRequestAction.INSTALL,
          failedRequestReason: PluginRequestFailure.PACKAGE_CHANGED,
        }),
      ],
    });

    const card = within(await cardOf('tool'));

    expect(card.getByText('Demande en échec')).toBeTruthy();
    expect(
      card.getByText(
        byText('L’installation n’a pas pu être appliquée : la copie du paquet a changé depuis votre confirmation.'),
      ),
    ).toBeTruthy();
  });

  it('stays usable when every plugin is disabled', async () => {
    const { bridge } = renderPage({
      plugins: [
        plugin('diagnostics-runtime', { status: PluginStatus.DISABLED }),
        plugin('diagnostics-clock', { status: PluginStatus.DISABLED }),
      ],
    });

    const card = within(await cardOf('diagnostics-runtime'));
    await userEvent.click(card.getByRole('button', { name: 'Activer' }));

    expect(bridge.enable).toHaveBeenCalledWith({ origin: 'shipped', pluginId: 'diagnostics-runtime' });
    expect(screen.getByRole('button', { name: 'Ajouter un paquet…' })).toBeTruthy();
  });

  it('offers "Enable" on a plugin whose disabling waits: it cancels both', async () => {
    const bridge = createBridge({
      listRequests: vi.fn(() =>
        Promise.resolve([{ action: 'disable', origin: 'shipped', pluginId: 'diagnostics-clock' }] as const),
      ),
    });
    renderPage({ plugins: [plugin('diagnostics-clock')], bridge });
    const card = within(await cardOf('diagnostics-clock'));

    await userEvent.click(await card.findByRole('button', { name: 'Activer' }));

    expect(card.queryByRole('button', { name: 'Désactiver' })).toBeNull();
    expect(bridge.enable).toHaveBeenCalledWith({ origin: 'shipped', pluginId: 'diagnostics-clock' });
  });

  it('offers only to cancel an uninstallation that waits', async () => {
    const bridge = createBridge({
      listRequests: vi.fn(() =>
        Promise.resolve([{ action: 'uninstall', origin: 'installed', pluginId: 'tool' }] as const),
      ),
    });
    renderPage({ plugins: [installed('tool')], bridge });
    const card = within(await cardOf('tool'));

    expect(await card.findByRole('button', { name: 'Annuler la demande' })).toBeTruthy();
    expect(card.queryByRole('button', { name: 'Désinstaller' })).toBeNull();
    expect(card.getByText('Sera désinstallé au prochain redémarrage.')).toBeTruthy();
  });

  it('says a waiting installation of an installed plugin replaces it, with the new package', async () => {
    const bridge = createBridge({
      listRequests: vi.fn(() =>
        Promise.resolve([
          { action: 'install', origin: 'installed', pluginId: 'tool', packageName: 'tool-2.0.0.voyagerplugin' },
        ] as const),
      ),
    });
    renderPage({ plugins: [installed('tool')], bridge });
    const card = within(await cardOf('tool'));

    expect(
      await card.findByText(
        byText('Sera remplacé au prochain redémarrage par le paquet « tool-2.0.0.voyagerplugin ».'),
      ),
    ).toBeTruthy();
    expect(screen.queryByText(byText('Nouveau plugin « tool »'))).toBeNull();
  });

  it('tells a shipped plugin from an installed one of the same identifier', async () => {
    const bridge = createBridge({
      listRequests: vi.fn(() =>
        Promise.resolve([{ action: 'disable', origin: 'shipped', pluginId: 'diagnostics-clock' }] as const),
      ),
    });
    renderPage({
      plugins: [
        plugin('diagnostics-clock'),
        installed('diagnostics-clock', {
          status: PluginStatus.REJECTED,
          rejectionReason: PluginRejectionReason.DUPLICATE_OF_SHIPPED,
        }),
      ],
      bridge,
    });

    const [shipped, copy] = await screen.findAllByText('diagnostics-clock');
    const shippedCard = within(cardAround(shipped));
    const installedCard = within(cardAround(copy));

    expect(await shippedCard.findByText('En attente de redémarrage')).toBeTruthy();
    expect(shippedCard.queryByRole('button', { name: 'Désinstaller' })).toBeNull();
    expect(installedCard.queryByText('En attente de redémarrage')).toBeNull();
    expect(
      installedCard.getByText(
        'Un plugin livré avec Voyager porte déjà cet identifiant. Désactivez-le pour utiliser celui-ci.',
      ),
    ).toBeTruthy();
    expect(installedCard.getByRole('button', { name: 'Désinstaller' })).toBeTruthy();
  });

  it('shows a request whose plugin is not in the list, and cancels it', async () => {
    const bridge = createBridge({
      listRequests: vi.fn(() =>
        Promise.resolve([{ action: 'disable', origin: 'installed', pluginId: 'gone' }] as const),
      ),
    });
    renderPage({ plugins: [plugin('diagnostics-clock')], bridge });

    const card = within(await cardOf('gone'));
    expect(card.getByText('Absent de la liste des plugins.')).toBeTruthy();
    await userEvent.click(card.getByRole('button', { name: 'Annuler la demande' }));

    expect(bridge.cancelRequest).toHaveBeenCalledWith({ origin: 'installed', pluginId: 'gone' });
  });

  it('shows no card for a request until the list is read', async () => {
    const bridge = createBridge({
      listRequests: vi.fn(() =>
        Promise.resolve([
          { action: 'install', origin: 'installed', pluginId: 'tool', packageName: 'tool-2.0.0.voyagerplugin' },
        ] as const),
      ),
    });
    renderPage({ plugins: [], bridge, listPlugins: () => new Promise(() => undefined) });

    expect(await screen.findByText(byText('Une demande attend le redémarrage.'))).toBeTruthy();
    expect(screen.queryByText(byText('Nouveau plugin « tool »'))).toBeNull();
  });

  it('says the waiting requests could not be read', async () => {
    const bridge = createBridge({ listRequests: vi.fn(() => Promise.reject(new Error('broken bridge'))) });
    renderPage({ plugins: [plugin('diagnostics-clock')], bridge });

    expect(await screen.findByText('Les demandes en attente n’ont pas pu être lues.')).toBeTruthy();
  });

  it('says there was nothing to restart, and reads the requests again', async () => {
    const bridge = createBridge({
      listRequests: vi.fn(() =>
        Promise.resolve([{ action: 'disable', origin: 'shipped', pluginId: 'diagnostics-clock' }] as const),
      ),
      restart: vi.fn(() => Promise.resolve({ outcome: 'nothing-to-apply' as const })),
    });
    renderPage({ plugins: [plugin('diagnostics-clock')], bridge });

    await userEvent.click(await screen.findByRole('button', { name: 'Redémarrer pour appliquer' }));

    expect(await screen.findByText('Aucune demande n’attend : rien à redémarrer.')).toBeTruthy();
    expect(bridge.listRequests).toHaveBeenCalledTimes(2);
  });

  it('says the service could not restart, and offers the restart again', async () => {
    const bridge = createBridge({
      listRequests: vi.fn(() =>
        Promise.resolve([{ action: 'disable', origin: 'shipped', pluginId: 'diagnostics-clock' }] as const),
      ),
      restart: vi.fn(() => Promise.reject(new Error('broken bridge'))),
    });
    renderPage({ plugins: [plugin('diagnostics-clock')], bridge });

    await userEvent.click(await screen.findByRole('button', { name: 'Redémarrer pour appliquer' }));

    expect(
      await screen.findByText('Le service n’a pas pu redémarrer. Le détail est dans le journal de Voyager.'),
    ).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Redémarrer pour appliquer' })).toBeTruthy();
  });

  it('starts no other operation while one is on its way', async () => {
    const bridge = createBridge({
      listInbox: vi.fn(() => Promise.resolve([PACKAGE])),
      install: vi.fn(() => new Promise<never>(() => undefined)),
    });
    renderPage({ plugins: [plugin('diagnostics-clock')], bridge });

    await userEvent.click(within(await cardOf('tool-1.0.0.voyagerplugin')).getByRole('button', { name: 'Installer' }));

    const disable = within(await cardOf('diagnostics-clock')).getByRole<HTMLButtonElement>('button', {
      name: 'Désactiver',
    });
    await waitFor(() => {
      expect(disable.disabled).toBe(true);
    });
    expect(screen.getByRole<HTMLButtonElement>('button', { name: 'Ajouter un paquet…' }).disabled).toBe(true);
  });

  it('is in English too', async () => {
    renderPage({ plugins: [installed('tool')], locale: 'en' });

    const card = within(await cardOf('tool'));

    expect(card.getByText('Installed by you')).toBeTruthy();
    expect(card.getByText('Not verified')).toBeTruthy();
    expect(card.getByRole('button', { name: 'Uninstall' })).toBeTruthy();
  });
});

describe('the inbox', () => {
  it('lists the packages of the inbox, and installs one by its name', async () => {
    const bridge = createBridge({ listInbox: vi.fn(() => Promise.resolve([PACKAGE])) });
    renderPage({ plugins: [], bridge });

    const card = within(await cardOf('tool-1.0.0.voyagerplugin'));
    expect(card.getByText(startingWith('20 Ko, modifié le'))).toBeTruthy();
    await userEvent.click(card.getByRole('button', { name: 'Installer' }));

    expect(bridge.install).toHaveBeenCalledWith('tool-1.0.0.voyagerplugin');
    expect(await screen.findByText('C’est noté : le changement sera appliqué au prochain redémarrage.')).toBeTruthy();
  });

  it('says why the examination refused a package', async () => {
    const bridge = createBridge({
      listInbox: vi.fn(() => Promise.resolve([PACKAGE])),
      install: vi.fn(() => Promise.resolve({ outcome: 'refused' as const, reason: 'forbidden-entry' as const })),
    });
    renderPage({ plugins: [], bridge });

    await userEvent.click(within(await cardOf('tool-1.0.0.voyagerplugin')).getByRole('button', { name: 'Installer' }));

    expect(
      await screen.findByText(byText('Ce paquet est refusé : il contient un fichier ou un nom qui n’est pas permis.')),
    ).toBeTruthy();
  });

  it('reads the inbox again when the window gets the focus back', async () => {
    const { bridge } = renderPage({ plugins: [] });
    await screen.findByText('Aucun paquet dans la boîte de dépôt.');

    window.dispatchEvent(new Event('focus'));

    await waitFor(() => {
      expect(bridge.listInbox).toHaveBeenCalledTimes(2);
    });
  });

  it('says the inbox is empty', async () => {
    renderPage({ plugins: [] });

    expect(await screen.findByText('Aucun paquet dans la boîte de dépôt.')).toBeTruthy();
  });

  it('adds a package the user chooses, and says so with its cleaned name', async () => {
    const bridge = createBridge({
      addPackage: vi.fn(() =>
        Promise.resolve({
          outcome: 'added' as const,
          name: 'tool\u202E.voyagerplugin',
          displayName: 'tool.voyagerplugin',
        }),
      ),
    });
    renderPage({ plugins: [], bridge });

    await userEvent.click(await screen.findByRole('button', { name: 'Ajouter un paquet…' }));

    expect(
      await screen.findByText(byText('Le paquet « tool.voyagerplugin » est dans la boîte de dépôt.')),
    ).toBeTruthy();
    expect(bridge.listInbox).toHaveBeenCalledTimes(2);
  });

  it('opens the inbox', async () => {
    const { bridge } = renderPage({ plugins: [] });

    await userEvent.click(await screen.findByRole('button', { name: 'Ouvrir la boîte de dépôt' }));

    expect(bridge.openInbox).toHaveBeenCalledTimes(1);
  });

  it('says the inbox could not be opened, without the message of the desktop app', async () => {
    const bridge = createBridge({ openInbox: vi.fn(() => Promise.reject(new Error('C:\\Users\\Traveler A'))) });
    renderPage({ plugins: [], bridge });

    await userEvent.click(await screen.findByRole('button', { name: 'Ouvrir la boîte de dépôt' }));

    expect(
      await screen.findByText('La boîte de dépôt n’a pas pu être ouverte. Le détail est dans le journal de Voyager.'),
    ).toBeTruthy();
    expect(screen.queryByText(/Traveler A/)).toBeNull();
  });
});
