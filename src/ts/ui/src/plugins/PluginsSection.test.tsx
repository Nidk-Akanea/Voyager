// @vitest-environment jsdom
import { timestampFromDate } from '@bufbuild/protobuf/wkt';
import { QueryClient } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import type { ComponentType } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { FactsOutcome, PluginRejectionReason, PluginStatus, type LocalServiceClients } from '@voyager/grpc-client';
import type { DiagnosticFactPanelProps, PluginRegistry } from '@voyager/plugin-sdk';

import { AppProviders } from '../app/AppProviders';
import { ShellProviders } from '../app/ShellProviders';
import type { Locale } from '../i18n/localization';
import type { PluginRuntime } from './pluginRuntime';
import { PluginsSection } from './PluginsSection';

const PROVIDES = ['diagnostic-fact-source'];

function plugin(id: string, extra: Record<string, unknown> = {}) {
  return {
    folder: id,
    id,
    version: '1.2.3',
    status: PluginStatus.LOADED,
    rejectionReason: PluginRejectionReason.UNSPECIFIED,
    uiModule: '',
    provides: PROVIDES,
    ...extra,
  };
}

function textFact(labelId: string, text: string) {
  return { labelId, value: { case: 'text' as const, value: text } };
}

function sourceFacts(sourceId: string, facts: readonly unknown[], outcome = FactsOutcome.SUCCEEDED, truncated = false) {
  return { sources: [{ sourceId, outcome, facts, truncated }] };
}

const MESSAGES: Readonly<Record<string, string>> = {
  'plugins/diagnostics-runtime/i18n/fr/messages.ftl': 'diagnostics-runtime-fact-cpu = Processeurs',
  'plugins/diagnostics-runtime/i18n/en/messages.ftl': 'diagnostics-runtime-fact-cpu = Processors',
  'plugins/diagnostics-clock/i18n/fr/messages.ftl': 'diagnostics-clock-fact-now = Maintenant',
  // A plugin whose identifier is the start of another one's: its messages are not the other plugin's.
  'plugins/diagnostics/i18n/fr/messages.ftl': 'diagnostics-clock-fact-now = Piraté',
};

function createRuntime(modules: Readonly<Record<string, unknown>> = {}): PluginRuntime {
  return {
    importModule: vi.fn((url: string) => {
      const module = modules[url];
      return module === undefined ? Promise.reject(new Error('not found')) : Promise.resolve(module);
    }),
    fetchText: (url) => Promise.resolve(MESSAGES[url.replace('test://', '')]),
    resolve: (path) => `test://${path}`,
  };
}

function renderSection(options: {
  plugins: readonly unknown[];
  facts?: Readonly<Record<string, unknown>>;
  modules?: Readonly<Record<string, unknown>>;
  locale?: Locale;
  listPlugins?: () => Promise<unknown>;
}) {
  const { plugins, facts = {}, modules = {}, locale = 'fr' } = options;
  const getDiagnosticFacts = vi.fn(({ sourceId }: { sourceId: string }) =>
    facts[sourceId] === undefined ? Promise.reject(new Error('no facts')) : Promise.resolve(facts[sourceId]),
  );
  const clients = {
    diagnostics: {},
    pluginHost: {
      listPlugins: vi.fn(options.listPlugins ?? (() => Promise.resolve({ plugins }))),
      getDiagnosticFacts,
    },
  } as unknown as LocalServiceClients;
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <ShellProviders locale={locale}>
      <AppProviders locale={locale} clients={clients} queryClient={queryClient} pluginRuntime={createRuntime(modules)}>
        <PluginsSection />
      </AppProviders>
    </ShellProviders>,
  );
  return { getDiagnosticFacts };
}

describe('PluginsSection', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('says so when no plugin is installed', async () => {
    renderSection({ plugins: [] });

    expect(await screen.findByText('Aucun plugin n’est installé.')).toBeTruthy();
  });

  it('shows a plugin without an interface part with the standard list, in the language of the user', async () => {
    renderSection({
      plugins: [plugin('diagnostics-runtime')],
      facts: {
        'diagnostics-runtime': sourceFacts('diagnostics-runtime', [textFact('diagnostics-runtime-fact-cpu', '8')]),
      },
      locale: 'en',
    });

    expect(await screen.findByText('Processors')).toBeTruthy();
    expect(screen.getByText('8')).toBeTruthy();
    expect(screen.getByText('Loaded')).toBeTruthy();
    expect(screen.getByText(/Version.*1.2.3/)).toBeTruthy();
  });

  it('falls back to French for a message the plugin does not translate', async () => {
    renderSection({
      plugins: [plugin('diagnostics-clock')],
      facts: { 'diagnostics-clock': sourceFacts('diagnostics-clock', [textFact('diagnostics-clock-fact-now', 'now')]) },
      locale: 'en',
    });

    expect(await screen.findByText('Maintenant')).toBeTruthy();
  });

  it('formats the instants and the numbers for the user', async () => {
    renderSection({
      plugins: [plugin('diagnostics-clock')],
      facts: {
        'diagnostics-clock': sourceFacts('diagnostics-clock', [
          { labelId: 'diagnostics-clock-fact-now', value: { case: 'number', value: 1234.5 } },
          {
            labelId: 'diagnostics-clock-fact-then',
            value: { case: 'instant', value: timestampFromDate(new Date('2026-03-14T09:30:00Z')) },
          },
        ]),
      },
    });

    expect(await screen.findByText(/1\s234,5/)).toBeTruthy();
    expect(screen.getByText(/2026/)).toBeTruthy();
  });

  it('draws the panel of a plugin instead of the standard list, and lets it ask for the facts again', async () => {
    const Panel: ComponentType<DiagnosticFactPanelProps> = ({ facts, refresh }) => (
      <button type="button" onClick={refresh}>
        Panel with {facts.length} fact
      </button>
    );
    const { getDiagnosticFacts } = renderSection({
      plugins: [plugin('diagnostics-clock', { uiModule: 'index.js' })],
      facts: { 'diagnostics-clock': sourceFacts('diagnostics-clock', [textFact('diagnostics-clock-fact-now', 'now')]) },
      modules: {
        'test://plugins/diagnostics-clock/ui/index.js': {
          register: (registry: PluginRegistry) => {
            registry.addDiagnosticFactPanel(Panel);
          },
        },
      },
    });

    const button = await screen.findByText('Panel with 1 fact');
    await userEvent.click(button);

    await waitFor(() => {
      expect(getDiagnosticFacts.mock.calls.length).toBe(2);
    });
    expect(screen.queryByText('Maintenant')).toBeNull();
  });

  it('shows a plugin whose panel crashes as being in error, and keeps the rest of the page', async () => {
    const Panel: ComponentType<DiagnosticFactPanelProps> = () => {
      throw new Error('broken panel');
    };
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    renderSection({
      plugins: [plugin('diagnostics-clock', { uiModule: 'index.js' }), plugin('diagnostics-runtime')],
      facts: {
        'diagnostics-clock': sourceFacts('diagnostics-clock', [textFact('diagnostics-clock-fact-now', 'now')]),
        'diagnostics-runtime': sourceFacts('diagnostics-runtime', [textFact('diagnostics-runtime-fact-cpu', '8')]),
      },
      modules: {
        'test://plugins/diagnostics-clock/ui/index.js': {
          register: (registry: PluginRegistry) => {
            registry.addDiagnosticFactPanel(Panel);
          },
        },
      },
    });

    expect(await screen.findByText('En erreur')).toBeTruthy();
    expect(screen.getByText('Son panneau a rencontré une erreur.')).toBeTruthy();
    expect(await screen.findByText('Processeurs')).toBeTruthy();
  });

  it('shows a plugin whose interface module cannot be loaded as being in error, without its facts', async () => {
    renderSection({
      plugins: [plugin('diagnostics-clock', { uiModule: 'index.js' })],
      facts: { 'diagnostics-clock': sourceFacts('diagnostics-clock', [textFact('diagnostics-clock-fact-now', 'now')]) },
    });

    expect(await screen.findByText('Son interface n’a pas pu se charger.')).toBeTruthy();
    expect(screen.getByText('En erreur')).toBeTruthy();
    expect(screen.queryByText('Maintenant')).toBeNull();
  });

  it.each([
    [FactsOutcome.FAILED, 'Sa source de faits a échoué.'],
    [FactsOutcome.TIMED_OUT, 'Sa source de faits n’a pas répondu à temps.'],
    [FactsOutcome.INVALID, 'Sa source de faits a répondu avec des données refusées.'],
  ])('says why a source that did not answer well is in error (%s)', async (outcome, message) => {
    renderSection({
      plugins: [plugin('diagnostics-runtime')],
      facts: { 'diagnostics-runtime': sourceFacts('diagnostics-runtime', [], outcome) },
    });

    expect(await screen.findByText(message)).toBeTruthy();
    expect(screen.getByText('En erreur')).toBeTruthy();
  });

  it('says why a plugin was set aside', async () => {
    renderSection({
      plugins: [
        plugin('broken', {
          status: PluginStatus.REJECTED,
          rejectionReason: PluginRejectionReason.SDK_INCOMPATIBLE,
          provides: [],
          version: '',
        }),
      ],
    });

    expect(await screen.findByText('Écarté')).toBeTruthy();
    expect(screen.getByText('Il est écrit pour une version du SDK que cette application n’accepte pas.')).toBeTruthy();
  });

  it('notes that facts were left out', async () => {
    renderSection({
      plugins: [plugin('diagnostics-runtime')],
      facts: {
        'diagnostics-runtime': sourceFacts(
          'diagnostics-runtime',
          [textFact('diagnostics-runtime-fact-cpu', '8')],
          FactsOutcome.SUCCEEDED,
          true,
        ),
      },
    });

    expect(await screen.findByText('D’autres faits existent mais ne sont pas affichés.')).toBeTruthy();
  });

  it('offers to try again when the list of plugins cannot be read', async () => {
    const { getDiagnosticFacts } = renderSection({
      plugins: [],
      listPlugins: () => Promise.reject(new Error('connection refused')),
    });

    expect(await screen.findByText('La liste des plugins n’a pas pu être lue.')).toBeTruthy();
    expect(getDiagnosticFacts).not.toHaveBeenCalled();
  });
});

describe('PluginsSection, what a plugin must not be able to do', () => {
  it('does not let a plugin whose identifier starts another one’s change the labels of that one', async () => {
    renderSection({
      plugins: [plugin('diagnostics'), plugin('diagnostics-clock')],
      facts: {
        diagnostics: sourceFacts('diagnostics', []),
        'diagnostics-clock': sourceFacts('diagnostics-clock', [textFact('diagnostics-clock-fact-now', 'now')]),
      },
    });

    expect(await screen.findByText('Maintenant')).toBeTruthy();
    expect(screen.queryByText('Piraté')).toBeNull();
  });

  it('shows a source whose outcome this version does not know as being in error, never as loaded', async () => {
    renderSection({
      plugins: [plugin('diagnostics-runtime')],
      facts: { 'diagnostics-runtime': sourceFacts('diagnostics-runtime', [], 99 as unknown as FactsOutcome) },
    });

    expect(await screen.findByText('Sa source de faits a échoué.')).toBeTruthy();
    expect(screen.getByText('En erreur')).toBeTruthy();
  });

  it('says that the reason is not known when the service gives one this version does not know', async () => {
    renderSection({
      plugins: [plugin('broken', { status: PluginStatus.REJECTED, rejectionReason: 99, provides: [] })],
    });

    expect(await screen.findByText('La raison n’est pas connue.')).toBeTruthy();
  });

  it('keeps a plugin that repeats a label from breaking the page', async () => {
    renderSection({
      plugins: [plugin('diagnostics-runtime')],
      facts: {
        'diagnostics-runtime': sourceFacts('diagnostics-runtime', [
          textFact('diagnostics-runtime-fact-cpu', '8'),
          textFact('diagnostics-runtime-fact-cpu', '16'),
        ]),
      },
    });

    expect(await screen.findByText('16')).toBeTruthy();
    expect(screen.getByText('8')).toBeTruthy();
  });
});
