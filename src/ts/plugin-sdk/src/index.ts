// SDK of the user interface for plugins (ADR-0015). A plugin's interface module imports this package, React,
// Fluent UI and @fluent/react, and nothing else: the host shares its own copy of each of them with the plugin,
// so there is only ever one React on the page.
import type { ComponentType } from 'react';

/** The extension points a plugin can provide, as written in the `provides` section of its manifest. */
export const EXTENSION_POINTS = {
  /** A source of diagnostic facts about the service, shown on the "About" page. */
  diagnosticFactSource: 'diagnostic-fact-source',
} as const;

/** The value of a diagnostic fact: a text, a number or an instant. */
export type FactValue =
  | { readonly kind: 'text'; readonly text: string }
  | { readonly kind: 'number'; readonly number: number }
  | { readonly kind: 'instant'; readonly instant: Date };

/** One piece of information about the service, as the user interface receives it. */
export interface DiagnosticFact {
  /** Identifier of the label's message in the translations of the plugin, for example `diagnostics-clock-fact-now`. */
  readonly labelId: string;
  /** The value, to write with `formatFactValue`. */
  readonly value: FactValue;
}

/** What the host gives the panel of a plugin: the facts of the plugin's source, and a way to ask again. */
export interface DiagnosticFactPanelProps {
  /** Identifier of the plugin the panel belongs to. */
  readonly pluginId: string;
  /** The language of the user, such as `fr` or `en`, to give to `formatFactValue`. */
  readonly locale: string;
  /** The facts of the plugin's source, in the order the plugin gave them. */
  readonly facts: readonly DiagnosticFact[];
  /** True when the plugin gave more facts than the host keeps. */
  readonly truncated: boolean;
  /** True while the facts are being asked again. */
  readonly isRefreshing: boolean;
  /** Asks the plugin's source for its facts again. */
  readonly refresh: () => void;
}

/** Where a plugin declares what its interface part provides. The host refuses what the manifest does not announce. */
export interface PluginRegistry {
  /**
   * Shows the facts of the plugin with this panel, instead of the standard list. Requires the extension point
   * `diagnostic-fact-source` in the plugin's manifest. A plugin has at most one panel.
   */
  readonly addDiagnosticFactPanel: (panel: ComponentType<DiagnosticFactPanelProps>) => void;
}

/**
 * What the interface module of a plugin exports under the name `register`. It runs once, synchronously, when the
 * host loads the module.
 *
 * @example
 * export const register: RegisterPlugin = (registry) => {
 *   registry.addDiagnosticFactPanel(MyPanel);
 * };
 */
export type RegisterPlugin = (registry: PluginRegistry) => void;

/**
 * Writes a fact value for the user's language: numbers with the local separators, instants in the user's time
 * zone. The service gives an instant and never a formatted text.
 *
 * @param value The value of a fact.
 * @param locale The language of the user, such as `fr` or `en` (`DiagnosticFactPanelProps.locale`).
 * @returns The text to show.
 * @example
 * formatFactValue({ kind: 'number', number: 1234.5 }, 'en'); // "1,234.5"
 */
export function formatFactValue(value: FactValue, locale: string): string {
  switch (value.kind) {
    case 'text':
      return value.text;
    case 'number':
      return new Intl.NumberFormat(locale).format(value.number);
    case 'instant':
      return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'medium' }).format(value.instant);
  }
}
