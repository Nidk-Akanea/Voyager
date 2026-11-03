// A request of the "Plugins" screen whose plugin has no card: a new plugin waiting to be installed, or a plugin that is
// no longer in the list of the service. It can only be cancelled.
import { Body1, Button, Caption1, Card, CardFooter, CardHeader, Subtitle2 } from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';

import type { PendingPluginRequest, PluginTarget } from '@voyager/grpc-client';

import { PENDING_MESSAGES } from './PluginRow';

/** The plugin a request names, and nothing more: the desktop app reads only that. */
function targetOfRequest({ origin, pluginId }: PendingPluginRequest): PluginTarget {
  return { origin, pluginId };
}

export interface UnlistedRequestCardProps {
  readonly request: PendingPluginRequest;
  /** Whether an operation is on its way: no other one starts meanwhile. */
  readonly busy: boolean;
  readonly onCancel: (target: PluginTarget) => void;
}

/** A waiting request without a plugin card: what it will do, the package it installs, and a way to cancel it. */
export function UnlistedRequestCard({ request, busy, onCancel }: UnlistedRequestCardProps) {
  const { l10n } = useLocalization();
  return (
    <Card>
      <CardHeader
        header={
          <Subtitle2>
            {request.action === 'install'
              ? l10n.getString('plugin-management-new-install', { id: request.pluginId })
              : request.pluginId}
          </Subtitle2>
        }
        description={
          <Caption1>
            {request.packageName === undefined
              ? l10n.getString('plugin-management-missing')
              : l10n.getString('plugin-management-new-install-package', { name: request.packageName })}
          </Caption1>
        }
      />
      <Body1>{l10n.getString(PENDING_MESSAGES[request.action])}</Body1>
      <CardFooter>
        <Button
          disabled={busy}
          onClick={() => {
            onCancel(targetOfRequest(request));
          }}
        >
          {l10n.getString('plugin-management-cancel-request')}
        </Button>
      </CardFooter>
    </Card>
  );
}
