import type { ComponentType } from 'react';
import { describe, expect, it, vi } from 'vitest';

import type { DiagnosticFactPanelProps, PluginRegistry } from '@voyager/plugin-sdk';

import { loadPluginUi, type PluginToLoad } from './loadPluginUi';
import type { PluginRuntime } from './pluginRuntime';

const PLUGIN: PluginToLoad = { id: 'diagnostics-clock', uiModule: 'index.js', provides: ['diagnostic-fact-source'] };
const PANEL: ComponentType<DiagnosticFactPanelProps> = () => null;

function runtimeThatImports(importModule: PluginRuntime['importModule']): PluginRuntime {
  return { importModule, fetchText: () => Promise.resolve(undefined), resolve: (path) => `voyager://app/${path}` };
}

describe('loadPluginUi', () => {
  it('has nothing to load for a plugin without an interface part', async () => {
    const importModule = vi.fn();

    const ui = await loadPluginUi({ ...PLUGIN, uiModule: '' }, runtimeThatImports(importModule));

    expect(ui).toEqual({ status: 'none' });
    expect(importModule).not.toHaveBeenCalled();
  });

  it('imports the module of the plugin from its own folder and keeps the panel it registers', async () => {
    const importModule = vi.fn<PluginRuntime['importModule']>(() =>
      Promise.resolve({
        register: (registry: PluginRegistry) => {
          registry.addDiagnosticFactPanel(PANEL);
        },
      }),
    );

    const ui = await loadPluginUi(PLUGIN, runtimeThatImports(importModule));

    expect(importModule).toHaveBeenCalledWith('voyager://app/plugins/diagnostics-clock/ui/index.js');
    expect(ui).toEqual({ status: 'ready', panel: PANEL });
  });

  it('is ready without a panel when the module registers nothing', async () => {
    const ui = await loadPluginUi(
      PLUGIN,
      runtimeThatImports(() => Promise.resolve({ register: () => undefined })),
    );

    expect(ui).toEqual({ status: 'ready', panel: undefined });
  });

  it.each([
    ['cannot be imported', () => Promise.reject(new Error('404'))],
    ['has no register function', () => Promise.resolve({})],
    ['is not an object', () => Promise.resolve(null)],
    [
      'throws while registering',
      () =>
        Promise.resolve({
          register: () => {
            throw new Error('broken');
          },
        }),
    ],
    [
      'registers a panel twice',
      () =>
        Promise.resolve({
          register: (registry: PluginRegistry) => {
            registry.addDiagnosticFactPanel(PANEL);
            registry.addDiagnosticFactPanel(PANEL);
          },
        }),
    ],
  ])('is failed when the module %s', async (_case, importModule) => {
    expect(await loadPluginUi(PLUGIN, runtimeThatImports(importModule))).toEqual({ status: 'failed' });
  });

  it('refuses a panel that the manifest does not announce', async () => {
    const ui = await loadPluginUi(
      { ...PLUGIN, provides: [] },
      runtimeThatImports(() =>
        Promise.resolve({
          register: (registry: PluginRegistry) => {
            registry.addDiagnosticFactPanel(PANEL);
          },
        }),
      ),
    );

    expect(ui).toEqual({ status: 'failed' });
  });
});

describe('loadPluginUi, against a plugin that misbehaves', () => {
  it('gives up on a module that never loads', async () => {
    const ui = await loadPluginUi(
      PLUGIN,
      runtimeThatImports(() => new Promise(() => undefined)),
      20,
    );

    expect(ui).toEqual({ status: 'failed' });
  });

  it('is failed when register is asynchronous, as its panel would be registered too late', async () => {
    const ui = await loadPluginUi(
      PLUGIN,
      runtimeThatImports(() =>
        Promise.resolve({
          register: async (registry: PluginRegistry) => {
            await Promise.resolve();
            registry.addDiagnosticFactPanel(PANEL);
          },
        }),
      ),
    );

    expect(ui).toEqual({ status: 'failed' });
  });

  it.each([
    ['a text', 'div'],
    ['nothing', undefined],
    ['null', null],
    ['a number', 42],
  ])('is failed when the panel is %s and not a component', async (_case, panel) => {
    const ui = await loadPluginUi(
      PLUGIN,
      runtimeThatImports(() =>
        Promise.resolve({
          register: (registry: PluginRegistry) => {
            registry.addDiagnosticFactPanel(panel as unknown as ComponentType<DiagnosticFactPanelProps>);
          },
        }),
      ),
    );

    expect(ui).toEqual({ status: 'failed' });
  });
});
