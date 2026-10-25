// The only door between the user interface and the desktop app (contextBridge). The interface
// never reaches Node.js, Electron or the file system (docs/conventions/typescript.md).
import { contextBridge, ipcRenderer } from 'electron';

import type { LocalServiceConnection } from '@voyager/grpc-client';

import { GET_LOCAL_SERVICE_CHANNEL } from '../shared/ipc';

// Same shape as VoyagerBridge in src/ts/ui/src/app/bridge.ts.
contextBridge.exposeInMainWorld('voyager', {
  getLocalService: () => ipcRenderer.invoke(GET_LOCAL_SERVICE_CHANNEL) as Promise<LocalServiceConnection>,
});
