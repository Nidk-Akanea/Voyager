// Values shared by every screen: the language, the server shown and its clients.
import { createContext, useContext } from 'react';

import type { RegisteredServer, VoyagerClients } from '@voyager/grpc-client';

import type { Locale } from '../i18n/localization';
import type { PluginRuntime } from '../plugins/pluginRuntime';

export interface AppContextValue {
  readonly locale: Locale;
  /** The server the screens show; undefined in tests that need none. */
  readonly server?: RegisteredServer;
  /** The clients of that server, through the main process. */
  readonly clients: VoyagerClients;
  /** How plugin modules and translations are loaded; tests give one that loads nothing from the network. */
  readonly pluginRuntime: PluginRuntime;
}

export const AppContext = createContext<AppContextValue | null>(null);

/** The language, the server and its clients, for the running app. */
export function useAppContext(): AppContextValue {
  const value = useContext(AppContext);
  if (value === null) {
    throw new Error('useAppContext must be used inside AppProviders.');
  }
  return value;
}
