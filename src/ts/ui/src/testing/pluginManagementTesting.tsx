// What the tests of the "Plugins" screen and of the shell share: plugins as the service lists them, a stand-in for
// the bridge of the desktop app, and the screen drawn with its providers.
import { timestampFromDate } from '@bufbuild/protobuf/wkt';
import { QueryClient } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { vi } from 'vitest';

import {
  ContractRole,
  ContractUseState,
  PluginOrigin,
  PluginRejectionReason,
  PluginRequestAction,
  PluginRequestFailure,
  PluginStatus,
  PluginVerification,
  type InboxPackage,
  type LocalServiceClients,
  type PendingPluginRequest,
  type PluginManagementBridge,
} from '@voyager/grpc-client';

import { AppProviders } from '../app/AppProviders';
import { AppShell } from '../app/AppShell';
import { ShellProviders } from '../app/ShellProviders';
import type { Locale } from '../i18n/localization';
import type { PluginRuntime } from '../plugins/pluginRuntime';
import { PluginsPage } from '../features/plugins/PluginsPage';

export const SHA256 = 'c'.repeat(64);

export function plugin(id: string, extra: Record<string, unknown> = {}) {
  return {
    folder: id,
    id,
    version: '1.2.3',
    status: PluginStatus.LOADED,
    rejectionReason: PluginRejectionReason.UNSPECIFIED,
    uiModule: '',
    provides: [],
    contracts: [{ contract: 'plugin', version: 1, role: ContractRole.REQUIRED, state: ContractUseState.SUPPORTED }],
    origin: PluginOrigin.SHIPPED,
    verification: PluginVerification.GUARANTEED_BY_APPLICATION,
    hasDotnet: true,
    hasUi: false,
    packageName: '',
    packageSha256: '',
    pendingRequest: PluginRequestAction.UNSPECIFIED,
    failedRequest: PluginRequestAction.UNSPECIFIED,
    failedRequestReason: PluginRequestFailure.UNSPECIFIED,
    ...extra,
  };
}

export function installed(id: string, extra: Record<string, unknown> = {}) {
  return plugin(id, {
    origin: PluginOrigin.INSTALLED,
    verification: PluginVerification.UNVERIFIED,
    installedAt: timestampFromDate(new Date('2026-05-04T08:30:00Z')),
    packageName: `${id}-1.2.3.voyagerplugin`,
    packageSha256: SHA256,
    ...extra,
  });
}

export const PACKAGE: InboxPackage = {
  name: 'tool-1.0.0.voyagerplugin',
  displayName: 'tool-1.0.0.voyagerplugin',
  size: 20_480,
  modifiedAt: new Date('2026-05-04T08:30:00Z').getTime(),
};

/** A text that holds variables: Fluent wraps each one in invisible isolation marks. */
export function withoutMarks(text: string | null | undefined): string {
  return (text ?? '').replace(/[\u2068\u2069]/g, '');
}

export function byText(expected: string) {
  return (_content: string, element: Element | null) =>
    element !== null &&
    withoutMarks(element.textContent) === expected &&
    ![...element.children].some((child) => withoutMarks(child.textContent) === expected);
}

export function startingWith(prefix: string) {
  return (_content: string, element: Element | null) =>
    element !== null &&
    withoutMarks(element.textContent).startsWith(prefix) &&
    ![...element.children].some((child) => withoutMarks(child.textContent).startsWith(prefix));
}

export function createBridge(overrides: Partial<PluginManagementBridge> = {}) {
  return {
    listInbox: vi.fn(() => Promise.resolve([] as readonly InboxPackage[])),
    addPackage: vi.fn(() => Promise.resolve({ outcome: 'cancelled' as const })),
    openInbox: vi.fn(() => Promise.resolve()),
    install: vi.fn(() => Promise.resolve({ outcome: 'requested' as const })),
    uninstall: vi.fn(() => Promise.resolve({ outcome: 'requested' as const })),
    enable: vi.fn(() => Promise.resolve({ outcome: 'requested' as const })),
    disable: vi.fn(() => Promise.resolve({ outcome: 'requested' as const })),
    cancelRequest: vi.fn(() => Promise.resolve({ outcome: 'nothing-to-apply' as const })),
    listRequests: vi.fn(() => Promise.resolve([] as readonly PendingPluginRequest[])),
    restart: vi.fn(() => Promise.resolve({ outcome: 'restarting' as const })),
    ...overrides,
  } satisfies PluginManagementBridge;
}

const RUNTIME: PluginRuntime = {
  importModule: () => Promise.reject(new Error('not in these tests')),
  fetchText: () => Promise.resolve(undefined),
  resolve: (path) => `test://${path}`,
};

export function renderPage(options: {
  plugins: readonly unknown[];
  bridge?: PluginManagementBridge;
  locale?: Locale;
  shell?: boolean;
  /** Answers the list instead of the plugins: a promise that never settles keeps it loading. */
  listPlugins?: () => Promise<unknown>;
}) {
  const { plugins, locale = 'fr', bridge = createBridge() } = options;
  const listPlugins = vi.fn(options.listPlugins ?? (() => Promise.resolve({ plugins })));
  const clients = {
    diagnostics: { getServiceInfo: () => new Promise(() => undefined) },
    pluginHost: {
      listPlugins,
      getDiagnosticFacts: () => Promise.resolve({ sources: [] }),
    },
  } as unknown as LocalServiceClients;
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <ShellProviders locale={locale}>
      <AppProviders locale={locale} clients={clients} queryClient={queryClient} pluginRuntime={RUNTIME}>
        {options.shell === true ? <AppShell pluginManagement={bridge} /> : <PluginsPage bridge={bridge} />}
      </AppProviders>
    </ShellProviders>,
  );
  return { bridge, listPlugins };
}

/** The card an element is in: the card itself, not its header, whose classes also start with "fui-Card". */
export function cardAround(element: Element | undefined): HTMLElement {
  const card = element?.closest('.fui-Card');
  if (!(card instanceof HTMLElement)) {
    throw new Error('The element is in no card.');
  }
  return card;
}

/** The card whose title is the given text. */
export async function cardOf(id: string): Promise<HTMLElement> {
  return cardAround(await screen.findByText(id));
}
