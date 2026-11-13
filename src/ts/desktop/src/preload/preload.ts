// The only door between the user interface and the desktop app (contextBridge). The interface
// never reaches Node.js, Electron or the file system (docs/conventions/typescript.md).
import { contextBridge, ipcRenderer, type IpcRendererEvent } from 'electron';

import type { RegisteredServer, ServerChoice, StartupState } from '@voyager/grpc-client';

import {
  GET_APP_VERSION_CHANNEL,
  GET_CURRENT_SERVER_CHANNEL,
  GET_SERVERS_CHANNEL,
  GET_STARTUP_STATE_CHANNEL,
  SELECT_SERVER_CHANNEL,
  STARTUP_STATE_CHANGED_CHANNEL,
} from '../shared/ipc';

// Same shape as VoyagerBridge in src/ts/ui/src/app/bridge.ts.
contextBridge.exposeInMainWorld('voyager', {
  getServers: () => ipcRenderer.invoke(GET_SERVERS_CHANNEL) as Promise<readonly ServerChoice[]>,
  getAppVersion: () => ipcRenderer.invoke(GET_APP_VERSION_CHANNEL) as Promise<string>,
  getCurrentServer: () => ipcRenderer.invoke(GET_CURRENT_SERVER_CHANNEL) as Promise<RegisteredServer | undefined>,
  selectServer: (serverId: string) => ipcRenderer.invoke(SELECT_SERVER_CHANNEL, serverId) as Promise<void>,
  getStartupState: () => ipcRenderer.invoke(GET_STARTUP_STATE_CHANNEL) as Promise<StartupState>,
  // The listener never sees the IPC event: it would give the page a handle on Electron's internals.
  onStartupStateChange: (listener: (state: StartupState) => void) => {
    const forward = (_event: IpcRendererEvent, state: StartupState): void => {
      listener(state);
    };
    ipcRenderer.on(STARTUP_STATE_CHANGED_CHANNEL, forward);
    return () => {
      ipcRenderer.removeListener(STARTUP_STATE_CHANGED_CHANNEL, forward);
    };
  },
});
