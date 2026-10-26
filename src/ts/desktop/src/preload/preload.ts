// The only door between the user interface and the desktop app (contextBridge). The interface
// never reaches Node.js, Electron or the file system (docs/conventions/typescript.md).
import { contextBridge, ipcRenderer, type IpcRendererEvent } from 'electron';

import type { LocalServiceState, StartupState } from '@voyager/grpc-client';

import {
  GET_LOCAL_SERVICE_STATE_CHANNEL,
  GET_STARTUP_STATE_CHANNEL,
  LOCAL_SERVICE_STATE_CHANGED_CHANNEL,
  RETRY_LOCAL_SERVICE_CHANNEL,
  STARTUP_STATE_CHANGED_CHANNEL,
} from '../shared/ipc';

// Same shape as VoyagerBridge in src/ts/ui/src/app/bridge.ts.
contextBridge.exposeInMainWorld('voyager', {
  getLocalServiceState: () => ipcRenderer.invoke(GET_LOCAL_SERVICE_STATE_CHANNEL) as Promise<LocalServiceState>,
  // The listener never sees the IPC event: it would give the page a handle on Electron's internals.
  onLocalServiceStateChange: (listener: (state: LocalServiceState) => void) => {
    const forward = (_event: IpcRendererEvent, state: LocalServiceState): void => {
      listener(state);
    };
    ipcRenderer.on(LOCAL_SERVICE_STATE_CHANGED_CHANNEL, forward);
    return () => {
      ipcRenderer.removeListener(LOCAL_SERVICE_STATE_CHANGED_CHANNEL, forward);
    };
  },
  getStartupState: () => ipcRenderer.invoke(GET_STARTUP_STATE_CHANNEL) as Promise<StartupState>,
  onStartupStateChange: (listener: (state: StartupState) => void) => {
    const forward = (_event: IpcRendererEvent, state: StartupState): void => {
      listener(state);
    };
    ipcRenderer.on(STARTUP_STATE_CHANGED_CHANNEL, forward);
    return () => {
      ipcRenderer.removeListener(STARTUP_STATE_CHANGED_CHANNEL, forward);
    };
  },
  retryLocalService: () => ipcRenderer.invoke(RETRY_LOCAL_SERVICE_CHANNEL) as Promise<void>,
});
