// Channels between the main process and the preload script. The main process only answers the user
// interface, and only pushes to its window.

/** Returns what the app is doing before its window opens (StartupState). */
export const GET_STARTUP_STATE_CHANNEL = 'voyager:get-startup-state';

/** Pushed to the startup screen each time the startup state changes. */
export const STARTUP_STATE_CHANGED_CHANNEL = 'voyager:startup-state-changed';

/** Returns the identifiers of the group servers the app knows (ServerChoice[]), for the selector; views only. */
export const GET_SERVERS_CHANNEL = 'voyager:get-servers';

/** Returns the version of the app, for the "About" screen; main window only. */
export const GET_APP_VERSION_CHANNEL = 'voyager:get-app-version';

/** Returns the server of the view that asks, with its address (RegisteredServer, or undefined for none). */
export const GET_CURRENT_SERVER_CHANNEL = 'voyager:get-current-server';

/** Shows the view of another server, named by its identifier; only the view shown may ask (the selector). */
export const SELECT_SERVER_CHANNEL = 'voyager:select-server';
