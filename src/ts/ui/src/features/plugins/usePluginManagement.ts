// What the "Plugins" screen reads and asks for. The list comes from the local service; the inbox, the requests and
// every change go through the desktop app (ADR-0016): the page never writes a request itself, and every change that
// adds or enables code waits for the user's answer in a native dialog of the desktop app.
import { useIsMutating, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import type { PluginManagementBridge } from '@voyager/grpc-client';

import { PLUGIN_LIST_KEY } from '../../plugins/usePluginHost';
import type { OutcomeMessage } from './outcomeMessages';

const INBOX_KEY = ['plugins', 'inbox'] as const;
const REQUESTS_KEY = ['plugins', 'requests'] as const;
// Every operation of the screen: while one runs, no other starts, wherever the user is in the app.
const OPERATION_KEY = ['plugins', 'operation'] as const;

/** The packages waiting in the inbox; read again by the panel when the window gets the focus back. */
export function useInbox(bridge: PluginManagementBridge) {
  return useQuery({
    queryKey: INBOX_KEY,
    queryFn: () => bridge.listInbox(),
    refetchOnWindowFocus: false,
  });
}

/**
 * The requests waiting for the next restart. The desktop app writes them alone (ADR-0016, decision 4): it is the one
 * source of what waits, a new plugin included, rather than the pending request the service lists with each plugin.
 */
export function usePendingRequests(bridge: PluginManagementBridge) {
  return useQuery({
    queryKey: REQUESTS_KEY,
    queryFn: () => bridge.listRequests(),
    refetchOnWindowFocus: false,
  });
}

/** Reads the requests again: after a restart that did not happen, for instance. */
export function useRefreshRequests() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: REQUESTS_KEY });
}

/**
 * An operation of the desktop app on plugins, which says how it went: once it is answered, the list, the inbox and the
 * requests are read again. The desktop app answers every outcome the user caused: only a broken bridge throws.
 */
export function usePluginOperation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: OPERATION_KEY,
    mutationFn: (task: () => Promise<OutcomeMessage | undefined>) => task(),
    onSettled: () =>
      Promise.all(
        [PLUGIN_LIST_KEY, INBOX_KEY, REQUESTS_KEY].map((queryKey) => queryClient.invalidateQueries({ queryKey })),
      ),
  });
}

/** Whether an operation of the desktop app on plugins is on its way, even one started before the screen was shown. */
export function useOperationInProgress(): boolean {
  return useIsMutating({ mutationKey: OPERATION_KEY }) > 0;
}
