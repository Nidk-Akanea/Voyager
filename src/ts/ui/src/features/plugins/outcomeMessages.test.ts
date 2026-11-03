import { describe, expect, it } from 'vitest';

import type { AddPackageResult, PluginActionResult, RestartResult } from '@voyager/grpc-client';

import { actionOutcome, addOutcome, CHANGE_FAILED, restartOutcome } from './outcomeMessages';

// What a newer desktop app could answer: this version of the page does not know it.
const UNKNOWN = { outcome: 'something-new' };

describe('actionOutcome', () => {
  it.each([
    [{ outcome: 'requested' }, 'success', 'plugin-management-outcome-requested'],
    [{ outcome: 'nothing-to-apply' }, 'info', 'plugin-management-outcome-nothing-to-apply'],
    [{ outcome: 'not-found' }, 'warning', 'plugin-management-outcome-not-found'],
    [{ outcome: 'not-allowed' }, 'warning', 'plugin-management-outcome-not-allowed'],
    [{ outcome: 'service-not-ready' }, 'warning', 'plugin-management-outcome-service-not-ready'],
    [{ outcome: 'failed' }, 'error', 'plugin-management-outcome-failed'],
  ] as const)('says %j', (result, intent, messageId) => {
    expect(actionOutcome(result)).toEqual({ intent, messageId });
  });

  it('says why a package is refused', () => {
    expect(actionOutcome({ outcome: 'refused', reason: 'unknown' })).toEqual({
      intent: 'error',
      messageId: 'plugin-management-refused',
      args: { reason: 'unknown' },
    });
  });

  it('says nothing when the user cancelled', () => {
    expect(actionOutcome({ outcome: 'cancelled' })).toBeUndefined();
  });

  it('takes an answer it does not know for a failure, never a success', () => {
    expect(actionOutcome(UNKNOWN as unknown as PluginActionResult)).toBe(CHANGE_FAILED);
  });
});

describe('addOutcome', () => {
  it('says the cleaned name of the package added', () => {
    expect(addOutcome({ outcome: 'added', name: 'raw', displayName: 'clean' })?.args).toEqual({ name: 'clean' });
  });

  it('says a copy that failed, and an answer it does not know, as a failure', () => {
    expect(addOutcome({ outcome: 'failed' })).toBe(CHANGE_FAILED);
    expect(addOutcome(UNKNOWN as unknown as AddPackageResult)).toBe(CHANGE_FAILED);
  });
});

describe('restartOutcome', () => {
  it('says nothing while the service restarts: the page reloads', () => {
    expect(restartOutcome({ outcome: 'restarting' })).toBeUndefined();
  });

  it('says there was nothing to restart', () => {
    expect(restartOutcome({ outcome: 'nothing-to-apply' })?.messageId).toBe(
      'plugin-management-outcome-nothing-to-restart',
    );
  });

  it('takes a failure, and an answer it does not know, for a failed restart', () => {
    expect(restartOutcome({ outcome: 'failed' })?.messageId).toBe('plugin-management-outcome-restart-failed');
    expect(restartOutcome(UNKNOWN as unknown as RestartResult)?.messageId).toBe(
      'plugin-management-outcome-restart-failed',
    );
  });
});
