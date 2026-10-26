// What the screens need once the local service is ready: its clients and the data cache. Drawn inside ShellProviders.
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useMemo, useState, type ReactNode } from 'react';

import type { LocalServiceClients } from '@voyager/grpc-client';

import type { Locale } from '../i18n/localization';
import { AppContext } from './appContext';

const LOCAL_CALL_RETRIES = 1;

export interface AppProvidersProps {
  readonly locale: Locale;
  /** Clients of the local service that is ready; a restarted service gets new ones. */
  readonly clients: LocalServiceClients;
  /** Data cache; tests pass one that does not retry failed calls. */
  readonly queryClient?: QueryClient;
  readonly children: ReactNode;
}

/** Gives its children the clients of the local service and the data cache. */
export function AppProviders({ locale, clients, queryClient: givenQueryClient, children }: AppProvidersProps) {
  // Calls stay on this machine: one retry is enough before telling the user something is wrong.
  const [defaultQueryClient] = useState(
    () => new QueryClient({ defaultOptions: { queries: { retry: LOCAL_CALL_RETRIES } } }),
  );
  const queryClient = givenQueryClient ?? defaultQueryClient;
  const context = useMemo(() => ({ locale, clients }), [locale, clients]);

  return (
    <QueryClientProvider client={queryClient}>
      <AppContext.Provider value={context}>{children}</AppContext.Provider>
    </QueryClientProvider>
  );
}
