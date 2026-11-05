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
import { ServerSelector } from './ServerSelector';
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

/**
 * Shows the server of this view (each server has its own view in the window), with the selector when the app knows
 * several; or says that none is configured.
 */
export function ServerGate({ bridge, locale, createClients = createServerClients, children }: ServerGateProps) {
  const styles = useStyles();
  const { l10n } = useLocalization();
  const known = useServers(bridge);
  const server = known?.current;
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
      <ServerSelector servers={known.servers} current={server} bridge={bridge} />
      <ServerConnection>{children}</ServerConnection>
    </AppProviders>
  );
}
