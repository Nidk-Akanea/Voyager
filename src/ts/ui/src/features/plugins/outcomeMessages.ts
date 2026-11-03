// What the "Plugins" screen tells the user once the desktop app answered: a message of plugin-management.ftl, and how
// it is shown. An answer this version of the page does not know is a failure, never a success.
import type { MessageBarIntent } from '@fluentui/react-components';

import type { AddPackageResult, PluginActionResult, RestartResult } from '@voyager/grpc-client';

/** A message to show after an operation. */
export interface OutcomeMessage {
  readonly intent: MessageBarIntent;
  readonly messageId: string;
  readonly args?: Readonly<Record<string, string>>;
}

/** What to say when a change could not be saved, or the desktop app could not even answer. */
export const CHANGE_FAILED: OutcomeMessage = { intent: 'error', messageId: 'plugin-management-outcome-failed' };

/** What to say when the inbox could not be opened. */
export const OPEN_FAILED: OutcomeMessage = { intent: 'error', messageId: 'plugin-management-outcome-open-failed' };

/** What to say once a request was answered; nothing when the user cancelled it. */
export function actionOutcome(result: PluginActionResult): OutcomeMessage | undefined {
  switch (result.outcome) {
    case 'requested':
      return { intent: 'success', messageId: 'plugin-management-outcome-requested' };
    case 'nothing-to-apply':
      return { intent: 'info', messageId: 'plugin-management-outcome-nothing-to-apply' };
    case 'cancelled':
      return undefined;
    case 'not-found':
      return { intent: 'warning', messageId: 'plugin-management-outcome-not-found' };
    case 'not-allowed':
      return { intent: 'warning', messageId: 'plugin-management-outcome-not-allowed' };
    case 'refused':
      return { intent: 'error', messageId: 'plugin-management-refused', args: { reason: result.reason } };
    case 'service-not-ready':
      return { intent: 'warning', messageId: 'plugin-management-outcome-service-not-ready' };
    default:
      return CHANGE_FAILED;
  }
}

/** What to say once a package was added to the inbox, or not; nothing when the user cancelled the choice. */
export function addOutcome(result: AddPackageResult): OutcomeMessage | undefined {
  switch (result.outcome) {
    case 'added':
      return { intent: 'success', messageId: 'plugin-management-outcome-added', args: { name: result.displayName } };
    case 'cancelled':
      return undefined;
    case 'refused':
      return { intent: 'error', messageId: 'plugin-management-refused', args: { reason: result.reason } };
    default:
      return CHANGE_FAILED;
  }
}

/** What to say once a restart was asked; nothing while the service restarts, since the page reloads. */
export function restartOutcome(result: RestartResult): OutcomeMessage | undefined {
  switch (result.outcome) {
    case 'restarting':
      return undefined;
    case 'nothing-to-apply':
      return { intent: 'info', messageId: 'plugin-management-outcome-nothing-to-restart' };
    case 'restart-elsewhere':
      return { intent: 'warning', messageId: 'plugin-management-outcome-restart-elsewhere' };
    default:
      return { intent: 'error', messageId: 'plugin-management-outcome-restart-failed' };
  }
}
