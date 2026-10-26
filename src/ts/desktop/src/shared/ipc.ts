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
