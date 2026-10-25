// Values shared by every screen: the language and the clients of the local service.
import { createContext, useContext } from 'react';

import type { LocalServiceClients } from '@voyager/grpc-client';

import type { Locale } from '../i18n/localization';

export interface AppContextValue {
  readonly locale: Locale;
  readonly clients: LocalServiceClients;
}

export const AppContext = createContext<AppContextValue | null>(null);

/** The language and local service clients of the running app. */
export function useAppContext(): AppContextValue {
  const value = useContext(AppContext);
  if (value === null) {
    throw new Error('useAppContext must be used inside AppProviders.');
  }
  return value;
}
