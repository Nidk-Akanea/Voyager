// Channels between the main process and the preload script. The main process only answers the user
// interface, and only pushes to its window.

/** Returns what the app is doing before its window opens (StartupState). */
export const GET_STARTUP_STATE_CHANNEL = 'voyager:get-startup-state';

/** Pushed to the startup screen each time the startup state changes. */
export const STARTUP_STATE_CHANGED_CHANNEL = 'voyager:startup-state-changed';

/** Returns the state of the local service (LocalServiceState). */
export const GET_LOCAL_SERVICE_STATE_CHANNEL = 'voyager:get-local-service-state';

/** Pushed to the user interface each time the state of the local service changes. */
export const LOCAL_SERVICE_STATE_CHANGED_CHANNEL = 'voyager:local-service-state-changed';

/** Starts the local service again after it failed. */
export const RETRY_LOCAL_SERVICE_CHANNEL = 'voyager:retry-local-service';

// Management of plugins (ADR-0016): only the main window may ask, and every change waits for a native confirmation.

/** Returns the packages of the inbox (InboxPackage[]). */
export const LIST_INBOX_CHANNEL = 'voyager:plugins:list-inbox';

/** Lets the user choose a package file and copies it into the inbox (AddPackageResult). */
export const ADD_PACKAGE_CHANNEL = 'voyager:plugins:add-package';

/** Opens the inbox in the file explorer. */
export const OPEN_INBOX_CHANNEL = 'voyager:plugins:open-inbox';

/** Installs a package of the inbox, named by its file name (PluginActionResult). */
export const INSTALL_PLUGIN_CHANNEL = 'voyager:plugins:install';

/** Uninstalls an installed plugin (PluginTarget → PluginActionResult). */
export const UNINSTALL_PLUGIN_CHANNEL = 'voyager:plugins:uninstall';

/** Enables a plugin (PluginTarget → PluginActionResult). */
export const ENABLE_PLUGIN_CHANNEL = 'voyager:plugins:enable';

/** Disables a plugin (PluginTarget → PluginActionResult). */
export const DISABLE_PLUGIN_CHANNEL = 'voyager:plugins:disable';

/** Cancels the request waiting for a plugin (PluginTarget → PluginActionResult). */
export const CANCEL_PLUGIN_REQUEST_CHANNEL = 'voyager:plugins:cancel-request';

/** Returns the requests waiting for the next restart (PendingPluginRequest[]). */
export const LIST_PLUGIN_REQUESTS_CHANNEL = 'voyager:plugins:list-requests';

/** Restarts the local service to apply the requests (RestartResult). */
export const RESTART_FOR_PLUGINS_CHANNEL = 'voyager:plugins:restart';
