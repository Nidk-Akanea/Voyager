// Loads the interface part of a plugin: imports its module, lets it declare what it provides, and keeps only what
// its manifest announces (ADR-0015, decision 1). A module that fails costs its plugin and nothing else.
import type { ComponentType } from 'react';

import { EXTENSION_POINTS, type DiagnosticFactPanelProps, type PluginRegistry } from '@voyager/plugin-sdk';

import { PLUGIN_LOAD_TIMEOUT_MS, pluginModulePath, withTimeout, type PluginRuntime } from './pluginRuntime';

/** What the interface needs to know of a plugin to load its interface part. */
export interface PluginToLoad {
  readonly id: string;
  /** Path of the module under the plugin's `ui` folder; empty when the plugin has no interface part. */
  readonly uiModule: string;
  /** The extension points the plugin's manifest announces. */
  readonly provides: readonly string[];
}

/** The outcome of loading the interface part of a plugin. */
export type PluginUi =
  /** The plugin has no interface part. */
  | { readonly status: 'none' }
  /** The module loaded and registered; the panel is absent when the plugin only has the standard list. */
  | { readonly status: 'ready'; readonly panel: ComponentType<DiagnosticFactPanelProps> | undefined }
  /** The module could not be loaded, threw while registering, or registered what its manifest does not announce. */
  | { readonly status: 'failed' };

function isThenable(value: unknown): boolean {
  return typeof (value as { then?: unknown } | null | undefined)?.then === 'function';
}

/** The `register` function the module exports; throws when there is none, as the module is not checked by TypeScript. */
function readRegister(module: unknown): (registry: PluginRegistry) => unknown {
  const register = (module as { readonly register?: unknown } | null)?.register;
  if (typeof register !== 'function') {
    throw new TypeError('The module does not export a register function.');
  }
  return register as (registry: PluginRegistry) => unknown;
}

/** A registry that collects what one plugin declares, and refuses what its manifest does not announce. */
function createRegistry(plugin: PluginToLoad): {
  registry: PluginRegistry;
  panel: () => ComponentType<DiagnosticFactPanelProps> | undefined;
} {
  let panel: ComponentType<DiagnosticFactPanelProps> | undefined;
  const registry: PluginRegistry = Object.freeze({
    addDiagnosticFactPanel: (candidate: ComponentType<DiagnosticFactPanelProps>) => {
      if (!plugin.provides.includes(EXTENSION_POINTS.diagnosticFactSource)) {
        throw new Error(`The manifest does not announce the extension point ${EXTENSION_POINTS.diagnosticFactSource}.`);
      }
      // The module is not checked by TypeScript: whatever it gives must be a component to be drawn.
      const given: unknown = candidate;
      if (typeof given !== 'function' && (typeof given !== 'object' || given === null)) {
        throw new TypeError('A panel is a React component.');
      }
      if (panel !== undefined) {
        throw new Error('A plugin has at most one diagnostic fact panel.');
      }
      panel = candidate;
    },
  });
  return { registry, panel: () => panel };
}

/**
 * Loads the interface part of a plugin that is in use. Never rejects: every failure of the plugin (module missing,
 * too slow, throwing, registering what its manifest does not announce) is reported as `{ status: 'failed' }`.
 */
export async function loadPluginUi(
  plugin: PluginToLoad,
  runtime: PluginRuntime,
  timeoutMs = PLUGIN_LOAD_TIMEOUT_MS,
): Promise<PluginUi> {
  if (plugin.uiModule === '') {
    return { status: 'none' };
  }
  try {
    const module = await withTimeout(
      runtime.importModule(runtime.resolve(pluginModulePath(plugin.id, plugin.uiModule))),
      timeoutMs,
    );
    const register = readRegister(module);
    const registration = createRegistry(plugin);
    // Registering is synchronous: a promise would register after the panel was taken, and be lost.
    const result = register(registration.registry);
    if (isThenable(result)) {
      throw new TypeError('register must not be asynchronous.');
    }
    return { status: 'ready', panel: registration.panel() };
  } catch (error) {
    // A plugin is a boundary: whatever it does wrong, the page still shows the rest. The page shows nothing of the
    // cause, which may hold anything the plugin wrote; the console of the window keeps its message for developers.
    console.warn(
      `The interface of plugin ${plugin.id} could not be used:`,
      error instanceof Error ? error.message : 'unknown',
    );
    return { status: 'failed' };
  }
}
