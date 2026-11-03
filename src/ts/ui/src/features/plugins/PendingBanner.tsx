// The banner of the "Plugins" screen while requests wait: nothing changes until a restart, which it offers.
import { Button, MessageBar, MessageBarActions, MessageBarBody, Spinner } from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';

export interface PendingBannerProps {
  /** How many requests wait for the next restart. */
  readonly count: number;
  /** Whether the restart is on its way: the page reloads once the service is ready again. */
  readonly restarting: boolean;
  /** Whether another operation is on its way. */
  readonly busy: boolean;
  readonly onRestart: () => void;
}

/** "n requests wait for a restart", with the button that applies them; nothing when none waits. */
export function PendingBanner({ count, restarting, busy, onRestart }: PendingBannerProps) {
  const { l10n } = useLocalization();
  if (count === 0) {
    return null;
  }

  return (
    <MessageBar intent="info">
      <MessageBarBody>{l10n.getString('plugin-management-pending-count', { count })}</MessageBarBody>
      <MessageBarActions>
        {restarting ? (
          <Spinner size="tiny" label={l10n.getString('plugin-management-restarting')} />
        ) : (
          <Button appearance="primary" disabled={busy} onClick={onRestart}>
            {l10n.getString('plugin-management-restart')}
          </Button>
        )}
      </MessageBarActions>
    </MessageBar>
  );
}
