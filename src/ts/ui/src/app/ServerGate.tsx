// Shows its children once the app reaches a group server, and says what is going on until then: no server configured,
// connecting, unreachable (with "Try again", and a new attempt every few seconds), app too old, not signed in (spec of
// the switch to client-server, "État de la connexion"). The calls go through the main process (ADR-0019, decision 4).
import {
  Button,
  makeStyles,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  Spinner,
  tokens,
} from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState, type ReactNode } from 'react';

import {
  createServerClients,
  describeServerProblem,
  type RegisteredServer,
  type VoyagerClients,
} from '@voyager/grpc-client';

import type { Locale } from '../i18n/localization';
import { useAppContext } from './appContext';
import { AppProviders } from './AppProviders';
import type { ServerBridge } from './bridge';

const useStyles = makeStyles({
  page: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingHorizontalXXL,
    maxWidth: '640px',
  },
});

/** How often the app asks again a server that does not answer, and a server that answers. */
export const RECONNECT_INTERVAL_MS = 5_000;
export const CHECK_INTERVAL_MS = 30_000;

/** The query that tells whether the server answers; the screens reload when it answers again. */
const CONNECTION_QUERY_KEY = ['server', 'connection'] as const;

const PROBLEM_MESSAGES = {
  unreachable: 'app-server-unreachable',
  'app-too-old': 'app-server-too-old',
  'not-signed-in': 'app-server-not-signed-in',
  failed: 'app-server-failed',
} as const;

export interface ServerGateProps {
  /** What the desktop app exposes: the servers it knows. */
  readonly bridge: ServerBridge;
  readonly locale: Locale;
  /** Creates the clients of a server; through the main process by default, fakes in tests. */
  readonly createClients?: (serverId: string) => VoyagerClients;
  /** Drawn, inside {@link AppProviders}, only while the server answers. */
  readonly children: ReactNode;
}

/** The servers the app knows; undefined until the main process answers. */
function useServers(bridge: ServerBridge): readonly RegisteredServer[] | undefined {
  const [servers, setServers] = useState<readonly RegisteredServer[] | undefined>();
  useEffect(() => {
    let cancelled = false;
    bridge.getServers().then(
      (known) => {
        if (!cancelled) {
          setServers(known);
        }
      },
      (error: unknown) => {
        console.error('The servers of the app are unknown:', error instanceof Error ? error.message : error);
        if (!cancelled) {
          setServers([]);
        }
      },
    );
    return () => {
      cancelled = true;
    };
  }, [bridge]);
  return servers;
}

/** Holds the screens back until the server answers, and keeps checking that it still does. */
function ServerConnection({ children }: { readonly children: ReactNode }) {
  const styles = useStyles();
  const { l10n } = useLocalization();
  const { clients } = useAppContext();
  const queryClient = useQueryClient();
  const connection = useQuery({
    queryKey: CONNECTION_QUERY_KEY,
    queryFn: ({ signal }) => clients.diagnostics.getServiceInfo({}, { signal }),
    retry: false,
    refetchInterval: (query) => (query.state.status === 'error' ? RECONNECT_INTERVAL_MS : CHECK_INTERVAL_MS),
  });

  // When the server answers again, what the screens show is read afresh (adjusted while drawing, as React advises).
  const [seenStatus, setSeenStatus] = useState(connection.status);
  if (connection.status !== seenStatus) {
    setSeenStatus(connection.status);
    if (seenStatus === 'error' && connection.status === 'success') {
      void queryClient.invalidateQueries({ predicate: (query) => query.queryKey[0] !== CONNECTION_QUERY_KEY[0] });
    }
  }

  if (connection.isSuccess) {
    return <>{children}</>;
  }
  if (connection.isPending) {
    return (
      <main className={styles.page}>
        <Spinner label={l10n.getString('app-server-connecting')} />
      </main>
    );
  }

  const problem = describeServerProblem(connection.error);
  return (
    <main className={styles.page}>
      <MessageBar intent={problem === 'unreachable' ? 'warning' : 'error'}>
        <MessageBarBody>{l10n.getString(PROBLEM_MESSAGES[problem])}</MessageBarBody>
        {problem !== 'app-too-old' && (
          <MessageBarActions>
            <Button disabled={connection.isFetching} onClick={() => void connection.refetch()}>
              {l10n.getString('app-retry')}
            </Button>
          </MessageBarActions>
        )}
      </MessageBar>
    </main>
  );
}

/** Chooses the server the screens show, and holds them back until it answers. One server for now (lot 2b adds more). */
export function ServerGate({ bridge, locale, createClients = createServerClients, children }: ServerGateProps) {
  const styles = useStyles();
  const { l10n } = useLocalization();
  const servers = useServers(bridge);
  const server = servers?.at(0);
  const clients = useMemo(() => (server === undefined ? undefined : createClients(server.id)), [server, createClients]);

  if (servers === undefined) {
    return (
      <main className={styles.page}>
        <Spinner label={l10n.getString('app-starting')} />
      </main>
    );
  }
  if (server === undefined || clients === undefined) {
    return (
      <main className={styles.page}>
        <MessageBar intent="warning">
          <MessageBarBody>{l10n.getString('app-no-server')}</MessageBarBody>
        </MessageBar>
      </main>
    );
  }

  // Another server starts from a new cache: nothing of one shows on the other.
  return (
    <AppProviders key={server.id} locale={locale} server={server} clients={clients}>
      <ServerConnection>{children}</ServerConnection>
    </AppProviders>
  );
}
