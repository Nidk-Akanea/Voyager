// Interface part of the diagnostics-clock plugin: what the page calls once it has imported this module.
import type { PluginRegistry } from '@voyager/plugin-sdk';

import { ClockPanel } from './ClockPanel';

/** Gives the host the panel of the plugin; the manifest announces `diagnostic-fact-source`, which this needs. */
export function register(registry: PluginRegistry): void {
  registry.addDiagnosticFactPanel(ClockPanel);
}
