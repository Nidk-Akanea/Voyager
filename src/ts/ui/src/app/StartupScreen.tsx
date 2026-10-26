// The screen of the small window shown while the app starts: looking for an update, downloading and
// installing it (the app then restarts), and starting the local service. Plain on purpose: the design
// of the app comes later. The desktop app opens it with ?screen=startup (src/ts/desktop/src/main/main.ts)
// and src/main.tsx reads that.
import { makeStyles, ProgressBar, Spinner, Text, Title2, tokens } from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';

import type { StartupState } from '@voyager/grpc-client';

import type { StartupBridge } from './bridge';
import { useStartupState } from './useStartupState';

const FULL_PERCENT = 100;

const useStyles = makeStyles({
  screen: {
    display: 'flex',
    flexDirection: 'column',
    justifyContent: 'center',
    gap: tokens.spacingVerticalL,
    minHeight: '100vh',
    boxSizing: 'border-box',
    padding: tokens.spacingHorizontalXXL,
  },
});

export interface StartupScreenProps {
  /** What the desktop app exposes: where the startup stands. */
  readonly bridge: StartupBridge;
}

/** The startup window's only screen: the app's name and where the startup stands. */
export function StartupScreen({ bridge }: StartupScreenProps) {
  const styles = useStyles();
  const { l10n } = useLocalization();
  const state = useStartupState(bridge);

  return (
    <main className={styles.screen}>
      <Title2 as="h1">{l10n.getString('app-title')}</Title2>
      <Progress state={state} />
    </main>
  );
}

/** One line of progress per step of the startup; the download also shows its percentage. */
function Progress({ state }: { readonly state: StartupState }) {
  const { l10n } = useLocalization();

  switch (state.status) {
    case 'checking-updates':
      return <Spinner label={l10n.getString('startup-checking-updates')} />;
    case 'downloading-update':
      return (
        <>
          <Text>{l10n.getString('startup-downloading-update', { version: state.version })}</Text>
          <ProgressBar value={state.percent} max={FULL_PERCENT} />
          <Text>{l10n.getString('startup-download-progress', { percent: state.percent })}</Text>
        </>
      );
    case 'installing-update':
      return <Spinner label={l10n.getString('startup-installing-update', { version: state.version })} />;
    case 'starting-service':
      return <Spinner label={l10n.getString('app-starting')} />;
  }
}
