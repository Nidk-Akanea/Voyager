// Shows its children once the local service is ready, and says what is going on until then: starting,
// restarting after a crash, or failed with a way to try again, or to start without the installed plugins.
import {
  Body1,
  Button,
  makeStyles,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  Spinner,
  tokens,
} from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';
import { useMemo, useState, type ReactNode } from 'react';

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

/** The messages that say why the start without the installed plugins did not happen. */
type RecoveryProblem = 'app-start-without-plugins-failed' | 'app-start-without-plugins-not-failed';

export interface LocalServiceGateProps {
  /** What the desktop app exposes: the state of the local service and the ways to start it again. */
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
  // What to say when the start without the installed plugins did not happen, and whether it is on its way.
  const [recoveryProblem, setRecoveryProblem] = useState<RecoveryProblem | undefined>();
  const [recovering, setRecovering] = useState(false);
  const connection = state.status === 'ready' ? state.connection : undefined;
  const clients = useMemo(
    () => (connection === undefined ? undefined : createLocalServiceClients(connection)),
    [connection],
  );

  // A message about an earlier failure has no place on the screen of a later one: forgotten as soon as the service
  // leaves the failed state (adjusted while drawing, as React advises, rather than in an effect).
  const [seenStatus, setSeenStatus] = useState(state.status);
  if (state.status !== seenStatus) {
    setSeenStatus(state.status);
    if (state.status !== 'failed') {
      setRecoveryProblem(undefined);
    }
  }

  if (connection !== undefined && clients !== undefined) {
    // A restarted service has a new address and secret: starting from a new cache keeps nothing of the old one.
    return (
      <AppProviders key={`${connection.url} ${connection.token}`} locale={locale} clients={clients}>
        {children}
      </AppProviders>
    );
  }

  const retry = (): void => {
    bridge.retryLocalService().catch((error: unknown) => {
      console.error('The local service could not be started again:', error instanceof Error ? error.message : error);
    });
  };

  // When an installed plugin keeps the service from starting, the user starts without them and enables them again one
  // by one (spec of the plugin management, "Quand Voyager plante à cause d'un plugin").
  const startWithoutInstalledPlugins = (): void => {
    setRecoveryProblem(undefined);
    setRecovering(true);
    bridge
      .startWithoutInstalledPlugins()
      .then((result) => {
        if (result.outcome === 'not-failed') {
          setRecoveryProblem('app-start-without-plugins-not-failed');
        } else if (result.outcome !== 'restarting') {
          setRecoveryProblem('app-start-without-plugins-failed');
        }
      })
      .catch(() => {
        setRecoveryProblem('app-start-without-plugins-failed');
      })
      .finally(() => {
        setRecovering(false);
      });
  };

  return (
    <main className={styles.page}>
      {state.status === 'failed' ? (
        <>
          <MessageBar intent="error">
            <MessageBarBody>{l10n.getString('app-local-service-failed')}</MessageBarBody>
            <MessageBarActions>
              <Button onClick={retry}>{l10n.getString('app-retry')}</Button>
              <Button disabled={recovering} onClick={startWithoutInstalledPlugins}>
                {l10n.getString('app-start-without-plugins')}
              </Button>
            </MessageBarActions>
          </MessageBar>
          <Body1>{l10n.getString('app-start-without-plugins-help')}</Body1>
          {recoveryProblem !== undefined && (
            <MessageBar intent="error">
              <MessageBarBody>{l10n.getString(recoveryProblem)}</MessageBarBody>
            </MessageBar>
          )}
        </>
      ) : (
        <Spinner
          label={l10n.getString(state.status === 'restarting' ? 'app-local-service-restarting' : 'app-starting')}
        />
      )}
    </main>
  );
}
