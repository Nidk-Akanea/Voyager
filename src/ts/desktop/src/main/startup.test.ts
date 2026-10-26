import { describe, expect, it, vi } from 'vitest';

import type { StartupState } from '@voyager/grpc-client';

import { runStartup, toStartupState, type StartupSteps } from './startup';
import type { UpdateEvent, UpdateOutcome } from './updates';

/** Steps that record what happened, in order, in `calls` and `states`. */
function fakeSteps(outcome: UpdateOutcome, events: readonly UpdateEvent[] = []) {
  const calls: string[] = [];
  const states: StartupState[] = [];
  const steps: StartupSteps = {
    updateApp: (report) => {
      calls.push('updateApp');
      events.forEach(report);
      return Promise.resolve(outcome);
    },
    startLocalService: () => {
      calls.push('startLocalService');
      return Promise.resolve();
    },
    report: (state) => states.push(state),
    openMainWindow: vi.fn(() => calls.push('openMainWindow')),
  };
  return { steps, calls, states };
}

describe('runStartup', () => {
  it('starts the service and opens the window when the app is up to date', async () => {
    const { steps, calls, states } = fakeSteps('up-to-date', [{ kind: 'checking' }]);

    const result = await runStartup(steps);

    expect(result).toBe('opened');
    expect(calls).toEqual(['updateApp', 'startLocalService', 'openMainWindow']);
    expect(states).toEqual([{ status: 'checking-updates' }, { status: 'starting-service' }]);
  });

  it('opens the app all the same when the update failed', async () => {
    const { steps, calls } = fakeSteps('failed');

    expect(await runStartup(steps)).toBe('opened');
    expect(calls).toEqual(['updateApp', 'startLocalService', 'openMainWindow']);
  });

  it('starts nothing, and opens nothing, when an update is on its way', async () => {
    const { steps, calls, states } = fakeSteps('restarting', [
      { kind: 'downloading', version: '0.3.0', percent: 50 },
      { kind: 'installing', version: '0.3.0' },
    ]);

    const result = await runStartup(steps);

    expect(result).toBe('restarting-for-update');
    expect(calls).toEqual(['updateApp']);
    expect(states).toEqual([
      { status: 'downloading-update', version: '0.3.0', percent: 50 },
      { status: 'installing-update', version: '0.3.0' },
    ]);
  });
});

describe('runStartup when the service cannot be started', () => {
  it('rejects, and opens nothing: the caller opens the window itself', async () => {
    const { steps } = fakeSteps('up-to-date');
    const failing: StartupSteps = { ...steps, startLocalService: () => Promise.reject(new Error('no service')) };

    await expect(runStartup(failing)).rejects.toThrow('no service');
    expect(steps.openMainWindow).not.toHaveBeenCalled();
  });
});

describe('toStartupState', () => {
  it('turns each step of an update into what the screen shows', () => {
    expect(toStartupState({ kind: 'checking' })).toEqual({ status: 'checking-updates' });
    expect(toStartupState({ kind: 'downloading', version: '1.0.0', percent: 7 })).toEqual({
      status: 'downloading-update',
      version: '1.0.0',
      percent: 7,
    });
    expect(toStartupState({ kind: 'installing', version: '1.0.0' })).toEqual({
      status: 'installing-update',
      version: '1.0.0',
    });
  });
});
