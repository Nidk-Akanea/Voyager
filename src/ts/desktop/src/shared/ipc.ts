// Channels between the main process and the preload script. The main process checks who calls them.

/** Returns where the local service listens and the token it expects (LocalServiceConnection). */
export const GET_LOCAL_SERVICE_CHANNEL = 'voyager:get-local-service';
