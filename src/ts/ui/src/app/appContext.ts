// Values shared by every screen: the language and the clients of the local service.
import { createContext, useContext } from 'react';

import type { LocalServiceClients } from '@voyager/grpc-client';

import type { Locale } from '../i18n/localization';
import type { PluginRuntime } from '../plugins/pluginRuntime';

export interface AppContextValue {
  readonly locale: Locale;
  readonly clients: LocalServiceClients;
  /** How plugin modules and translations are loaded; tests give one that loads nothing from the network. */
  readonly pluginRuntime: PluginRuntime;
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
