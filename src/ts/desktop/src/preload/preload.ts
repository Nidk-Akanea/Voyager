// The only door between the user interface and the desktop app (contextBridge). The interface
// never reaches Node.js, Electron or the file system (docs/conventions/typescript.md).
import { contextBridge, ipcRenderer, type IpcRendererEvent } from 'electron';

import type {
  AddPackageResult,
  InboxPackage,
  LocalServiceState,
  PendingPluginRequest,
  PluginActionResult,
  PluginManagementBridge,
  PluginTarget,
  RecoveryResult,
  RestartResult,
  StartupState,
} from '@voyager/grpc-client';

import {
  ADD_PACKAGE_CHANNEL,
  CANCEL_PLUGIN_REQUEST_CHANNEL,
  DISABLE_PLUGIN_CHANNEL,
  ENABLE_PLUGIN_CHANNEL,
  GET_LOCAL_SERVICE_STATE_CHANNEL,
  GET_STARTUP_STATE_CHANNEL,
  INSTALL_PLUGIN_CHANNEL,
  LIST_INBOX_CHANNEL,
  LIST_PLUGIN_REQUESTS_CHANNEL,
  LOCAL_SERVICE_STATE_CHANGED_CHANNEL,
  OPEN_INBOX_CHANNEL,
  RESTART_FOR_PLUGINS_CHANNEL,
  RETRY_LOCAL_SERVICE_CHANNEL,
  START_WITHOUT_INSTALLED_PLUGINS_CHANNEL,
  STARTUP_STATE_CHANGED_CHANNEL,
  UNINSTALL_PLUGIN_CHANNEL,
} from '../shared/ipc';

// The page names a package by its name in the inbox and a plugin by its origin and identifier: never a path. The
// main process checks both, and asks the user before any change.
const plugins: PluginManagementBridge = {
  listInbox: () => ipcRenderer.invoke(LIST_INBOX_CHANNEL) as Promise<readonly InboxPackage[]>,
  addPackage: () => ipcRenderer.invoke(ADD_PACKAGE_CHANNEL) as Promise<AddPackageResult>,
  openInbox: () => ipcRenderer.invoke(OPEN_INBOX_CHANNEL) as Promise<void>,
  install: (name: string) => ipcRenderer.invoke(INSTALL_PLUGIN_CHANNEL, name) as Promise<PluginActionResult>,
  uninstall: (target: PluginTarget) =>
    ipcRenderer.invoke(UNINSTALL_PLUGIN_CHANNEL, target) as Promise<PluginActionResult>,
  enable: (target: PluginTarget) => ipcRenderer.invoke(ENABLE_PLUGIN_CHANNEL, target) as Promise<PluginActionResult>,
  disable: (target: PluginTarget) => ipcRenderer.invoke(DISABLE_PLUGIN_CHANNEL, target) as Promise<PluginActionResult>,
  cancelRequest: (target: PluginTarget) =>
    ipcRenderer.invoke(CANCEL_PLUGIN_REQUEST_CHANNEL, target) as Promise<PluginActionResult>,
  listRequests: () => ipcRenderer.invoke(LIST_PLUGIN_REQUESTS_CHANNEL) as Promise<readonly PendingPluginRequest[]>,
  restart: () => ipcRenderer.invoke(RESTART_FOR_PLUGINS_CHANNEL) as Promise<RestartResult>,
};

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
  startWithoutInstalledPlugins: () =>
    ipcRenderer.invoke(START_WITHOUT_INSTALLED_PLUGINS_CHANNEL) as Promise<RecoveryResult>,
  plugins,
});
