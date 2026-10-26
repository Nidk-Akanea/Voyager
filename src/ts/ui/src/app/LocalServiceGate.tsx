// Shows its children once the local service is ready, and says what is going on until then: starting,
// restarting after a crash, or failed with a way to try again.
import { Button, makeStyles, MessageBar, MessageBarBody, Spinner, tokens } from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';
import { useMemo, type ReactNode } from 'react';

import { createLocalServiceClients } from '@voyager/grpc-client';

import type { Locale } from '../i18n/localization';
import { AppProviders } from './AppProviders';
import type { LocalServiceBridge } from './bridge';
import { useLocalServiceState } from './useLocalServiceState';

const useStyles = makeStyles({
  page: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingHorizontalXXL,
    maxWidth: '640px',
  },
});

export interface LocalServiceGateProps {
  /** What the desktop app exposes: the state of the local service and the way to start it again. */
  readonly bridge: LocalServiceBridge;
  readonly locale: Locale;
  /** Drawn, inside {@link AppProviders}, only while the local service is ready. */
  readonly children: ReactNode;
}

/** Holds the screens back until the local service is ready, and tells the user what is going on meanwhile. */
export function LocalServiceGate({ bridge, locale, children }: LocalServiceGateProps) {
  const styles = useStyles();
  const { l10n } = useLocalization();
  const state = useLocalServiceState(bridge);
  const connection = state.status === 'ready' ? state.connection : undefined;
  const clients = useMemo(
    () => (connection === undefined ? undefined : createLocalServiceClients(connection)),
    [connection],
  );

  if (connection !== undefined && clients !== undefined) {
    // A restarted service has a new address and secret: starting from a new cache keeps nothing of the old one.
    return (
      <AppProviders key={`${connection.url} ${connection.token}`} locale={locale} clients={clients}>
        {children}
      </AppProviders>
    );
  }

  return (
    <main className={styles.page}>
      {state.status === 'failed' ? (
        <MessageBar intent="error">
          <MessageBarBody>{l10n.getString('app-local-service-failed')}</MessageBarBody>
          <Button
            onClick={() => {
              bridge.retryLocalService().catch((error: unknown) => {
                console.error(
                  'The local service could not be started again:',
                  error instanceof Error ? error.message : error,
                );
              });
            }}
          >
            {l10n.getString('app-retry')}
          </Button>
        </MessageBar>
      ) : (
        <Spinner
          label={l10n.getString(state.status === 'restarting' ? 'app-local-service-restarting' : 'app-starting')}
        />
      )}
    </main>
  );
}
