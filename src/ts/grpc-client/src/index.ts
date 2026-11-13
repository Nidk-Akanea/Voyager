// Typed clients for the Voyager services, generated from proto/ (docs/conventions/grpc.md).
import type { Client } from '@connectrpc/connect';

import type { DiagnosticsService } from './gen/voyager/diagnostics/v1/diagnostics_pb.js';
import type { PluginHostService } from './gen/voyager/pluginhost/v1/pluginhost_pb.js';

export * from './gen/voyager/diagnostics/v1/diagnostics_pb.js';
export * from './gen/voyager/pluginhost/v1/pluginhost_pb.js';
export * from './servers.js';
// The errors of a call, for the screens that tell them apart (and their tests), without depending on Connect themselves.
export { Code, ConnectError } from '@connectrpc/connect';

/**
 * What the app is doing before its window opens: looking for a newer version, and installing it, which restarts the
 * app. The startup screen shows it.
 */
export type StartupState =
  | { readonly status: 'checking-updates' }
  | { readonly status: 'downloading-update'; readonly version: string; readonly percent: number }
  | { readonly status: 'installing-update'; readonly version: string };

/** Clients for every service the screens call, on a group server. */
export interface VoyagerClients {
  readonly diagnostics: Client<typeof DiagnosticsService>;
  readonly pluginHost: Client<typeof PluginHostService>;
}
