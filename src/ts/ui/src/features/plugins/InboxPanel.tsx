// The inbox on the "Plugins" screen: the packages waiting to be installed, with a way to add one, to open the folder
// for packages dropped by hand, and to install one. The page names a package by its name, never by a path.
import {
  Body1,
  Button,
  Caption1,
  Card,
  CardHeader,
  MessageBar,
  MessageBarBody,
  Spinner,
  Subtitle2,
  Title3,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';
import { useEffect } from 'react';

import type { InboxPackage, PluginManagementBridge } from '@voyager/grpc-client';

import { useAppContext } from '../../app/appContext';
import { useInbox } from './usePluginManagement';

const useStyles = makeStyles({
  section: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  buttons: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: tokens.spacingHorizontalS,
  },
});

const BYTES_PER_KILOBYTE = 1024;

export interface InboxPanelProps {
  readonly bridge: PluginManagementBridge;
  /** Whether an operation is on its way: no other one starts meanwhile. */
  readonly busy: boolean;
  readonly onAdd: () => void;
  readonly onOpen: () => void;
  readonly onInstall: (name: string) => void;
}

/** The packages of the inbox, and the buttons to add, open and install. */
export function InboxPanel({ bridge, busy, onAdd, onOpen, onInstall }: InboxPanelProps) {
  const styles = useStyles();
  const { l10n } = useLocalization();
  const { locale } = useAppContext();
  const inbox = useInbox(bridge);
  const { refetch } = inbox;

  // The user drops packages in the inbox from the file explorer, then comes back to Voyager. Its document stays visible
  // all along, which TanStack Query's own focus tracking waits for: the focus of the window is listened to instead.
  useEffect(() => {
    const readAgain = (): void => {
      void refetch();
    };
    window.addEventListener('focus', readAgain);
    return () => {
      window.removeEventListener('focus', readAgain);
    };
  }, [refetch]);

  // Formatting only: a size in kilobytes, never less than one, and a date in the user's language.
  const describe = (entry: InboxPackage) =>
    l10n.getString('plugin-management-inbox-package', {
      size: new Intl.NumberFormat(locale, { maximumFractionDigits: 0 }).format(
        Math.max(1, Math.round(entry.size / BYTES_PER_KILOBYTE)),
      ),
      date: new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'short' }).format(entry.modifiedAt),
    });

  return (
    <section className={styles.section}>
      <Title3 as="h2">{l10n.getString('plugin-management-inbox-title')}</Title3>
      <Body1>{l10n.getString('plugin-management-inbox-help')}</Body1>
      <div className={styles.buttons}>
        <Button appearance="primary" disabled={busy} onClick={onAdd}>
          {l10n.getString('plugin-management-inbox-add')}
        </Button>
        <Button disabled={busy} onClick={onOpen}>
          {l10n.getString('plugin-management-inbox-open')}
        </Button>
        <Button disabled={inbox.isFetching} onClick={() => void refetch()}>
          {l10n.getString('plugin-management-inbox-refresh')}
        </Button>
      </div>
      {inbox.isPending && <Spinner size="tiny" label={l10n.getString('plugin-management-inbox-loading')} />}
      {inbox.isError && (
        <MessageBar intent="error">
          <MessageBarBody>{l10n.getString('plugin-management-inbox-error')}</MessageBarBody>
        </MessageBar>
      )}
      {inbox.isSuccess && inbox.data.length === 0 && <Body1>{l10n.getString('plugin-management-inbox-empty')}</Body1>}
      {inbox.data?.map((entry) => (
        <Card key={entry.name}>
          <CardHeader
            header={<Subtitle2>{entry.displayName}</Subtitle2>}
            description={<Caption1>{describe(entry)}</Caption1>}
            action={
              <Button
                disabled={busy}
                onClick={() => {
                  onInstall(entry.name);
                }}
              >
                {l10n.getString('plugin-management-inbox-install')}
              </Button>
            }
          />
        </Card>
      ))}
    </section>
  );
}
