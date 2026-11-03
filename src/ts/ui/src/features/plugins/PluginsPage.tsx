// "Plugins" screen (spec of the plugin management): every plugin Voyager knows, with its origin, verification and
// state, the inbox, and the requests waiting for a restart. It depends on no plugin: it stays usable when they are all
// disabled. Nothing changes before a restart, and every change that adds or enables code waits for the user's answer
// in a native dialog of the desktop app.
import {
  Body1,
  Button,
  MessageBar,
  MessageBarBody,
  Spinner,
  Title2,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';
import { useState } from 'react';

import type {
  PendingPluginRequest,
  Plugin,
  PluginActionResult,
  PluginManagementBridge,
  PluginTarget,
  RestartResult,
} from '@voyager/grpc-client';

import { usePluginList } from '../../plugins/usePluginHost';
import { InboxPanel } from './InboxPanel';
import {
  actionOutcome,
  addOutcome,
  CHANGE_FAILED,
  OPEN_FAILED,
  restartOutcome,
  type OutcomeMessage,
} from './outcomeMessages';
import { PendingBanner } from './PendingBanner';
import { PluginRow, targetOf, type PluginAction } from './PluginRow';
import { UnlistedRequestCard } from './UnlistedRequestCard';
import {
  useOperationInProgress,
  usePendingRequests,
  usePluginOperation,
  useRefreshRequests,
} from './usePluginManagement';

const useStyles = makeStyles({
  page: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingHorizontalXXL,
    maxWidth: '760px',
  },
  section: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
});

function sameTarget(request: PluginTarget, target: PluginTarget | undefined): boolean {
  return request.origin === target?.origin && request.pluginId === target.pluginId;
}

/** The requests whose plugin has no card: a new plugin, or one no longer in the list. Only once the list is read. */
function requestsWithoutCard(
  requests: readonly PendingPluginRequest[],
  plugins: readonly Plugin[] | undefined,
): readonly PendingPluginRequest[] {
  if (plugins === undefined) {
    return [];
  }
  return requests.filter((request) => !plugins.some((plugin) => sameTarget(request, targetOf(plugin))));
}

export interface PluginsPageProps {
  /** What the desktop app lets the page do with plugins. */
  readonly bridge: PluginManagementBridge;
}

/** The "Plugins" screen. */
export function PluginsPage({ bridge }: PluginsPageProps) {
  const styles = useStyles();
  const { l10n } = useLocalization();
  const list = usePluginList();
  const requests = usePendingRequests(bridge);
  const refreshRequests = useRefreshRequests();
  const operation = usePluginOperation();
  const inProgress = useOperationInProgress();
  const [message, setMessage] = useState<OutcomeMessage | undefined>();
  const [restarting, setRestarting] = useState(false);
  const busy = inProgress || restarting;

  /** Runs an operation of the desktop app, then says how it went. */
  const run = (task: () => Promise<OutcomeMessage | undefined>, failure = CHANGE_FAILED): void => {
    setMessage(undefined);
    operation.mutate(task, {
      onSuccess: setMessage,
      // The desktop app answers every outcome: only a broken bridge throws, and its message is never shown.
      onError: () => {
        setMessage(failure);
      },
    });
  };

  const act = (action: PluginAction, target: PluginTarget): void => {
    const calls: Readonly<Record<PluginAction, (target: PluginTarget) => Promise<PluginActionResult>>> = {
      enable: bridge.enable,
      disable: bridge.disable,
      uninstall: bridge.uninstall,
      'cancel-request': bridge.cancelRequest,
    };
    run(async () => actionOutcome(await calls[action](target)));
  };

  const restart = (): void => {
    setMessage(undefined);
    setRestarting(true);
    void bridge
      .restart()
      // Only a broken bridge throws: the restart did not happen.
      .catch((): RestartResult => ({ outcome: 'failed' }))
      .then((result) => {
        // Once the service restarted, the desktop app reloads the page: nothing more to say.
        if (result.outcome !== 'restarting') {
          setRestarting(false);
          setMessage(restartOutcome(result));
          void refreshRequests();
        }
      });
  };

  const pending = requests.data ?? [];
  const plugins = list.data?.plugins;
  const withoutCard = requestsWithoutCard(pending, plugins);

  return (
    <main className={styles.page}>
      <Title2 as="h1">{l10n.getString('plugin-management-title')}</Title2>
      {message !== undefined && (
        <MessageBar intent={message.intent}>
          <MessageBarBody>{l10n.getString(message.messageId, message.args)}</MessageBarBody>
        </MessageBar>
      )}
      {requests.isError && (
        <MessageBar intent="error">
          <MessageBarBody>{l10n.getString('plugin-management-requests-error')}</MessageBarBody>
          <Button onClick={() => void requests.refetch()}>{l10n.getString('plugin-management-retry')}</Button>
        </MessageBar>
      )}
      <PendingBanner count={pending.length} restarting={restarting} busy={busy} onRestart={restart} />
      <section className={styles.section}>
        {list.isPending && <Spinner label={l10n.getString('plugin-management-loading')} />}
        {list.isError && (
          <MessageBar intent="error">
            <MessageBarBody>{l10n.getString('plugin-management-error')}</MessageBarBody>
            <Button onClick={() => void list.refetch()}>{l10n.getString('plugin-management-retry')}</Button>
          </MessageBar>
        )}
        {plugins?.length === 0 && withoutCard.length === 0 && <Body1>{l10n.getString('plugin-management-none')}</Body1>}
        {plugins?.map((plugin) => {
          const target = targetOf(plugin);
          return (
            <PluginRow
              // A shipped plugin and an installed one may have folders of the same name.
              key={`${String(plugin.origin)}-${plugin.folder}`}
              plugin={plugin}
              pending={pending.find((request) => sameTarget(request, target))}
              busy={busy}
              onAction={act}
            />
          );
        })}
        {withoutCard.map((request) => (
          <UnlistedRequestCard
            key={`request-${request.origin}-${request.pluginId}`}
            request={request}
            busy={busy}
            onCancel={(target) => {
              act('cancel-request', target);
            }}
          />
        ))}
      </section>
      <InboxPanel
        bridge={bridge}
        busy={busy}
        onAdd={() => {
          run(async () => addOutcome(await bridge.addPackage()));
        }}
        onOpen={() => {
          run(async () => {
            await bridge.openInbox();
            return undefined;
          }, OPEN_FAILED);
        }}
        onInstall={(name) => {
          run(async () => actionOutcome(await bridge.install(name)));
        }}
      />
    </main>
  );
}
