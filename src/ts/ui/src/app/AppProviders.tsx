// What the screens need once a server is chosen: its clients and the data cache. Drawn inside ShellProviders.
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useMemo, useState, type ReactNode } from 'react';

import type { RegisteredServer, VoyagerClients } from '@voyager/grpc-client';

import type { Locale } from '../i18n/localization';
import { browserPluginRuntime, type PluginRuntime } from '../plugins/pluginRuntime';
import { AppContext } from './appContext';

const CALL_RETRIES = 1;

export interface AppProvidersProps {
  readonly locale: Locale;
  /** The server the screens show, for the "About" screen; none in tests that need none. */
  readonly server?: RegisteredServer;
  /** The version of the app, for the "About" screen. */
  readonly appVersion?: string;
  /** Clients of that server. */
  readonly clients: VoyagerClients;
  /** Data cache; tests pass one that does not retry failed calls. */
  readonly queryClient?: QueryClient;
  /** How plugin modules and translations are loaded; the browser by default, a fake in tests. */
  readonly pluginRuntime?: PluginRuntime;
  readonly children: ReactNode;
}

/** Gives its children the clients of the server shown and the data cache. */
export function AppProviders({
  locale,
  server,
  appVersion,
  clients,
  queryClient: givenQueryClient,
  pluginRuntime = browserPluginRuntime,
  children,
}: AppProvidersProps) {
  // One retry is enough before telling the user something is wrong: the connection state says the rest.
  const [defaultQueryClient] = useState(
    () => new QueryClient({ defaultOptions: { queries: { retry: CALL_RETRIES } } }),
  );
  const queryClient = givenQueryClient ?? defaultQueryClient;
  const context = useMemo(
    () => ({ locale, server, appVersion, clients, pluginRuntime }),
    [locale, server, appVersion, clients, pluginRuntime],
  );

  return (
    <QueryClientProvider client={queryClient}>
      <AppContext.Provider value={context}>{children}</AppContext.Provider>
    </QueryClientProvider>
  );
}
