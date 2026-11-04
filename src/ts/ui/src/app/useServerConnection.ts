// Whether the server shown answers (spec of the switch to client-server, "État de la connexion"): asked at once, then
// every few seconds while it does not answer, and every half minute while it does.
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';

import { describeServerProblem } from '@voyager/grpc-client';

import { useAppContext } from './appContext';

/** How often the app asks again a server that does not answer, and a server that answers. */
export const RECONNECT_INTERVAL_MS = 5_000;
export const CHECK_INTERVAL_MS = 30_000;

// A passing hiccup (a second of lost Wi-Fi) is tried again once before the connection counts as lost; a refusal of the
// server (app too old, not signed in) is final at once.
const CHECK_RETRIES = 1;

/** The query that tells whether the server answers; the screens' own queries are read again when it answers again. */
export const CONNECTION_QUERY_KEY = ['server', 'connection'] as const;

export function useServerConnection() {
  const { clients } = useAppContext();
  const queryClient = useQueryClient();
  const connection = useQuery({
    queryKey: CONNECTION_QUERY_KEY,
    queryFn: ({ signal }) => clients.diagnostics.getServiceInfo({}, { signal }),
    retry: (failures, error) => failures < CHECK_RETRIES && describeServerProblem(error) === 'unreachable',
    refetchInterval: (query) => (query.state.status === 'error' ? RECONNECT_INTERVAL_MS : CHECK_INTERVAL_MS),
  });

  // When the server answers again after it did not, what the screens show is read afresh.
  const previousStatus = useRef(connection.status);
  useEffect(() => {
    if (previousStatus.current === 'error' && connection.status === 'success') {
      void queryClient.invalidateQueries({ predicate: (query) => query.queryKey[0] !== CONNECTION_QUERY_KEY[0] });
    }
    previousStatus.current = connection.status;
  }, [connection.status, queryClient]);

  return connection;
}
