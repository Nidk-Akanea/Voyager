// Holds the screens back until the server answers, and says what is going on: connecting, unreachable (with "Try
// again"), app too old, not signed in. Once the server has answered, a later problem shows above the screens, which stay:
// a passing loss of the network never throws away what the user sees.
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
import type { ReactNode } from 'react';

import { describeServerProblem } from '@voyager/grpc-client';

import { useServerConnection } from './useServerConnection';

const useStyles = makeStyles({
  page: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingHorizontalXXL,
    maxWidth: '640px',
  },
  banner: {
    paddingInline: tokens.spacingHorizontalXL,
    paddingTop: tokens.spacingVerticalS,
  },
});

const PROBLEM_MESSAGES = {
  unreachable: 'app-server-unreachable',
  'app-too-old': 'app-server-too-old',
  'not-signed-in': 'app-server-not-signed-in',
  failed: 'app-server-failed',
} as const;

export function ServerConnection({ children }: { readonly children: ReactNode }) {
  const styles = useStyles();
  const { l10n } = useLocalization();
  const connection = useServerConnection();

  if (connection.isPending) {
    return (
      <main className={styles.page}>
        <Spinner label={l10n.getString('app-server-connecting')} />
      </main>
    );
  }

  const problem = connection.isError ? describeServerProblem(connection.error) : undefined;
  const message =
    problem === undefined ? undefined : (
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
    );

  // The server has answered before: the screens stay, with the problem above them.
  if (connection.data !== undefined) {
    return (
      <>
        {message !== undefined && <div className={styles.banner}>{message}</div>}
        {children}
      </>
    );
  }

  return <main className={styles.page}>{message}</main>;
}
