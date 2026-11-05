// Chooses the group server the screens show, gives them its clients, and lets ServerConnection hold them back until it
// answers (spec of the switch to client-server). The calls go through the main process (ADR-0019, decision 4).
import { makeStyles, MessageBar, MessageBarBody, Spinner, tokens } from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';
import { useMemo, type ReactNode } from 'react';

import { createServerClients, type VoyagerClients } from '@voyager/grpc-client';

import type { Locale } from '../i18n/localization';
import { AppProviders } from './AppProviders';
import type { ServerBridge } from './bridge';
import { ServerConnection } from './ServerConnection';
import { useServers } from './useServers';

const useStyles = makeStyles({
  page: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingHorizontalXXL,
    maxWidth: '640px',
  },
});

export interface ServerGateProps {
  /** What the desktop app exposes: the servers it knows, and its version. */
  readonly bridge: ServerBridge;
  readonly locale: Locale;
  /** Creates the clients of a server; through the main process by default, fakes in tests. */
  readonly createClients?: (serverId: string) => VoyagerClients;
  /** Drawn, inside {@link AppProviders}, once the server has answered. */
  readonly children: ReactNode;
}

/** Shows the first server the app knows (one view per server comes with lot 2b), or says that none is configured. */
export function ServerGate({ bridge, locale, createClients = createServerClients, children }: ServerGateProps) {
  const styles = useStyles();
  const { l10n } = useLocalization();
  const known = useServers(bridge);
  const server = known?.servers.at(0);
  const clients = useMemo(() => (server === undefined ? undefined : createClients(server.id)), [server, createClients]);

  if (known === undefined) {
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
    <AppProviders key={server.id} locale={locale} server={server} appVersion={known.appVersion} clients={clients}>
      <ServerConnection>{children}</ServerConnection>
    </AppProviders>
  );
}
