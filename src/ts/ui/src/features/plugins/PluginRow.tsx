// One plugin of the "Plugins" screen: its identifier, version, origin, verification and state, its detail, and what
// the user can do with it. Every action goes to the desktop app, which asks the user when the action needs it.
import {
  Badge,
  type BadgeProps,
  Body1,
  Button,
  Caption1,
  Card,
  CardFooter,
  CardHeader,
  Subtitle2,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { useLocalization, type ReactLocalization } from '@fluent/react';

import {
  PluginOrigin,
  PluginRequestAction,
  PluginRequestFailure,
  PluginStatus,
  PluginVerification,
  type PendingPluginRequest,
  type Plugin,
  type PluginRequestActionName,
  type PluginTarget,
} from '@voyager/grpc-client';

import { RejectionDetails } from '../../plugins/RejectionDetails';
import { PluginDetails } from './PluginDetails';

const useStyles = makeStyles({
  badges: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: tokens.spacingHorizontalXS,
  },
  version: {
    color: tokens.colorNeutralForeground3,
  },
});

/** What the user can ask for a plugin. */
export type PluginAction = 'enable' | 'disable' | 'uninstall' | 'cancel-request';

interface BadgeText {
  readonly color: BadgeProps['color'];
  readonly messageId: string;
}

const STATUS_BADGES: Readonly<Partial<Record<PluginStatus, BadgeText>>> = {
  [PluginStatus.LOADED]: { color: 'success', messageId: 'plugin-management-state-loaded' },
  [PluginStatus.REJECTED]: { color: 'warning', messageId: 'plugin-management-state-rejected' },
  [PluginStatus.DISABLED]: { color: 'subtle', messageId: 'plugin-management-state-disabled' },
};

const ORIGIN_MESSAGES: Readonly<Partial<Record<PluginOrigin, string>>> = {
  [PluginOrigin.SHIPPED]: 'plugin-management-origin-shipped',
  [PluginOrigin.INSTALLED]: 'plugin-management-origin-installed',
};

const VERIFICATION_BADGES: Readonly<Partial<Record<PluginVerification, BadgeText>>> = {
  [PluginVerification.GUARANTEED_BY_APPLICATION]: {
    color: 'brand',
    messageId: 'plugin-management-verification-guaranteed',
  },
};

// Anything the application does not vouch for, including a value this version of the page does not know.
const UNVERIFIED_BADGE: BadgeText = { color: 'warning', messageId: 'plugin-management-verification-unverified' };

/** What a request waiting for a plugin will do, said on its card. */
export const PENDING_MESSAGES: Readonly<Record<PluginRequestActionName, string>> = {
  install: 'plugin-management-pending-install',
  uninstall: 'plugin-management-pending-uninstall',
  enable: 'plugin-management-pending-enable',
  disable: 'plugin-management-pending-disable',
};

const ACTION_NAMES: Readonly<Partial<Record<PluginRequestAction, string>>> = {
  [PluginRequestAction.INSTALL]: 'install',
  [PluginRequestAction.UNINSTALL]: 'uninstall',
  [PluginRequestAction.ENABLE]: 'enable',
  [PluginRequestAction.DISABLE]: 'disable',
};

const FAILURE_NAMES: Readonly<Partial<Record<PluginRequestFailure, string>>> = {
  [PluginRequestFailure.NOT_FOUND]: 'not-found',
  [PluginRequestFailure.NOT_ALLOWED]: 'not-allowed',
  [PluginRequestFailure.FILE_SYSTEM_ERROR]: 'file-system-error',
  [PluginRequestFailure.NOT_SUPPORTED]: 'not-supported',
  [PluginRequestFailure.PACKAGE_MISSING]: 'package-missing',
  [PluginRequestFailure.PACKAGE_CHANGED]: 'package-changed',
  [PluginRequestFailure.PACKAGE_REFUSED]: 'package-refused',
  [PluginRequestFailure.NOT_ENOUGH_SPACE]: 'not-enough-space',
};

/** The plugin a request names, the way the desktop app names it; undefined for an origin this page does not know. */
export function targetOf(plugin: Pick<Plugin, 'origin' | 'folder'>): PluginTarget | undefined {
  if (plugin.origin === PluginOrigin.SHIPPED) {
    return { origin: 'shipped', pluginId: plugin.folder };
  }
  return plugin.origin === PluginOrigin.INSTALLED ? { origin: 'installed', pluginId: plugin.folder } : undefined;
}

/**
 * Whether the plugin will be disabled once the waiting request applies: the action offered is the one that changes
 * that, so that enabling a plugin whose disabling waits cancels both (spec, "Désinstaller, activer, désactiver").
 */
function willBeDisabled(plugin: Plugin, pending: PendingPluginRequest | undefined): boolean {
  if (pending?.action === 'disable') {
    return true;
  }
  return plugin.status === PluginStatus.DISABLED && pending?.action !== 'enable';
}

/** What the card says of the request waiting for the plugin. */
function pendingText(pending: PendingPluginRequest, l10n: ReactLocalization): string {
  // An installation of a plugin already listed replaces it.
  if (pending.action === 'install' && pending.packageName !== undefined) {
    return l10n.getString('plugin-management-pending-replace', { name: pending.packageName });
  }
  return l10n.getString(PENDING_MESSAGES[pending.action]);
}

export interface PluginRowProps {
  readonly plugin: Plugin;
  /** The request waiting for this plugin, if any. */
  readonly pending: PendingPluginRequest | undefined;
  /** Whether an operation is on its way: no other one starts meanwhile. */
  readonly busy: boolean;
  readonly onAction: (action: PluginAction, target: PluginTarget) => void;
}

/** One plugin, with its state, its detail and its actions. */
export function PluginRow({ plugin, pending, busy, onAction }: PluginRowProps) {
  const styles = useStyles();
  const { l10n } = useLocalization();
  const target = targetOf(plugin);
  const status = STATUS_BADGES[plugin.status];
  const origin = ORIGIN_MESSAGES[plugin.origin];
  const verification = VERIFICATION_BADGES[plugin.verification] ?? UNVERIFIED_BADGE;
  const failedAction = ACTION_NAMES[plugin.failedRequest];
  const installed = plugin.origin === PluginOrigin.INSTALLED;

  const action = (name: PluginAction, messageId: string) =>
    target === undefined ? undefined : (
      <Button
        disabled={busy}
        onClick={() => {
          onAction(name, target);
        }}
      >
        {l10n.getString(messageId)}
      </Button>
    );

  return (
    <Card>
      <CardHeader
        header={<Subtitle2>{plugin.id}</Subtitle2>}
        description={
          plugin.version === '' ? undefined : (
            <Caption1 className={styles.version}>
              {l10n.getString('plugin-management-version', { version: plugin.version })}
            </Caption1>
          )
        }
      />
      <div className={styles.badges}>
        {origin !== undefined && <Badge appearance="outline">{l10n.getString(origin)}</Badge>}
        <Badge appearance="outline" color={verification.color}>
          {l10n.getString(verification.messageId)}
        </Badge>
        {status !== undefined && <Badge color={status.color}>{l10n.getString(status.messageId)}</Badge>}
        {pending !== undefined && (
          <Badge color="informative">{l10n.getString('plugin-management-state-pending')}</Badge>
        )}
        {failedAction !== undefined && <Badge color="danger">{l10n.getString('plugin-management-state-failed')}</Badge>}
      </div>
      {plugin.status === PluginStatus.REJECTED && <RejectionDetails plugin={plugin} />}
      {pending !== undefined && <Body1>{pendingText(pending, l10n)}</Body1>}
      {failedAction !== undefined && (
        <Body1>
          {l10n.getString('plugin-management-failed', {
            action: failedAction,
            reason: FAILURE_NAMES[plugin.failedRequestReason] ?? 'other',
          })}
        </Body1>
      )}
      <PluginDetails plugin={plugin} />
      <CardFooter>
        {willBeDisabled(plugin, pending)
          ? action('enable', 'plugin-management-enable')
          : action('disable', 'plugin-management-disable')}
        {/* A shipped plugin can be disabled, never uninstalled; an uninstallation that waits is only cancelled. */}
        {installed && pending?.action !== 'uninstall' && action('uninstall', 'plugin-management-uninstall')}
        {pending !== undefined && action('cancel-request', 'plugin-management-cancel-request')}
      </CardFooter>
    </Card>
  );
}
