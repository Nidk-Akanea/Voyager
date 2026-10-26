import { afterEach, describe, expect, it, vi } from 'vitest';

import type { LocalServiceState } from '@voyager/grpc-client';

import { attachToRunningLocalService, launchLocalService } from './localService';
import { superviseLocalService, type LocalServiceSupervisor } from './localServiceSupervisor';

// Stand-ins for the .NET local service, run by the Node.js that runs the tests.
const ANNOUNCES_AND_WAITS = `
  process.stdout.write('VOYAGER_LISTENING http://127.0.0.1:4321\\n');
  process.stdin.resume();
  process.stdin.on('end', () => process.exit(0));
`;
const ANNOUNCES_THEN_DIES = `
  process.stdout.write('VOYAGER_LISTENING http://127.0.0.1:4321\\n');
  setTimeout(() => process.exit(1), 50);
`;
const FAILS_AT_STARTUP = 'process.exit(3);';
const NEVER_ANNOUNCES = 'setInterval(() => undefined, 1000);';
// Like the real service, stops when the app closes its standard input.
const STARTS_SLOWLY = `
  process.stdin.resume();
  process.stdin.on('end', () => process.exit(0));
  setInterval(() => undefined, 1000);
`;

const SHORT_DELAYS_MS = [10, 10];
const WAIT_MS = 5_000;

let supervisors: LocalServiceSupervisor[] = [];

/** Supervises stand-ins: the nth launch runs the nth script, the last one running for every launch after it. */
function supervise(
  scripts: readonly string[],
  options: {
    restartDelaysMs?: readonly number[];
    restartWindowMs?: number;
    now?: () => number;
    startupTimeoutMs?: number;
  } = {},
) {
  const reports: string[] = [];
  let launches = 0;
  const { startupTimeoutMs, ...supervisorOptions } = options;
  const supervisor = superviseLocalService({
    launch: () => {
      const script = scripts[Math.min(launches++, scripts.length - 1)] ?? '';
      return launchLocalService({ executable: process.execPath, args: ['-e', script], startupTimeoutMs });
    },
    report: (message) => reports.push(message),
    restartDelaysMs: SHORT_DELAYS_MS,
    ...supervisorOptions,
  });
  supervisors.push(supervisor);
  return { supervisor, reports, launches: () => launches };
}

function states(supervisor: LocalServiceSupervisor): LocalServiceState[] {
  const seen: LocalServiceState[] = [supervisor.state()];
  supervisor.onChange((state) => seen.push(state));
  return seen;
}

function waitForStatus(supervisor: LocalServiceSupervisor, status: LocalServiceState['status']) {
  return vi.waitFor(
    () => {
      expect(supervisor.state().status).toBe(status);
    },
    { timeout: WAIT_MS },
  );
}

afterEach(async () => {
  await Promise.all(supervisors.map((supervisor) => supervisor.stop()));
  supervisors = [];
});

describe('superviseLocalService', () => {
  it('is ready with the address the service announces', async () => {
    const { supervisor } = supervise([ANNOUNCES_AND_WAITS]);

    expect(supervisor.state()).toEqual({ status: 'starting' });
    await waitForStatus(supervisor, 'ready');

    expect(supervisor.state()).toMatchObject({ status: 'ready', connection: { url: 'http://127.0.0.1:4321' } });
  });

  it('starts the service again when it dies, with a new token', async () => {
    const { supervisor, launches } = supervise([ANNOUNCES_THEN_DIES, ANNOUNCES_AND_WAITS]);
    const seen = states(supervisor);

    await vi.waitFor(
      () => {
        expect(launches()).toBe(2);
      },
      { timeout: WAIT_MS },
    );
    await vi.waitFor(
      () => {
        expect(seen.filter((state) => state.status === 'ready')).toHaveLength(2);
      },
      {
        timeout: WAIT_MS,
      },
    );

    expect(seen.map((state) => state.status)).toEqual(['starting', 'ready', 'restarting', 'ready']);
    const [first, second] = seen.flatMap((state) => (state.status === 'ready' ? [state.connection.token] : []));
    expect(first).not.toBe(second);
  });

  it('gives up when it keeps dying, and says why only to the report', async () => {
    const { supervisor, reports, launches } = supervise([ANNOUNCES_THEN_DIES]);

    await waitForStatus(supervisor, 'failed');

    expect(launches()).toBe(3);
    expect(reports.at(-1)).toContain('3 times within 60 s');
  });

  it('forgets deaths older than the window', async () => {
    let clock = 0;
    const { supervisor, launches } = supervise([ANNOUNCES_THEN_DIES], {
      restartWindowMs: 1_000,
      now: () => (clock += 5_000),
    });

    await vi.waitFor(
      () => {
        expect(launches()).toBeGreaterThanOrEqual(5);
      },
      { timeout: WAIT_MS },
    );

    expect(supervisor.state().status).not.toBe('failed');
  });

  it('fails at once when the service cannot start, without restarting it', async () => {
    const { supervisor, reports, launches } = supervise([FAILS_AT_STARTUP]);

    await waitForStatus(supervisor, 'failed');

    expect(launches()).toBe(1);
    expect(reports.join('\n')).toContain('exit code 3');
  });

  it('fails, and stops the service, when it never announces its address', async () => {
    const { supervisor, reports } = supervise([NEVER_ANNOUNCES], { startupTimeoutMs: 200 });

    await waitForStatus(supervisor, 'failed');

    expect(reports.join('\n')).toContain('did not start');
  });

  it('fails when the service cannot even be launched', () => {
    const reports: string[] = [];
    const supervisor = superviseLocalService({
      launch: () => {
        throw new Error('no such executable');
      },
      report: (message) => reports.push(message),
    });
    supervisors.push(supervisor);

    expect(supervisor.state()).toEqual({ status: 'failed' });
    expect(reports.join('\n')).toContain('no such executable');
    supervisor.retry();
    expect(supervisor.state()).toEqual({ status: 'failed' });
  });

  it('keeps supervising when a listener fails', async () => {
    const { supervisor, reports, launches } = supervise([ANNOUNCES_THEN_DIES, ANNOUNCES_AND_WAITS]);
    supervisor.onChange(() => {
      throw new Error('window destroyed');
    });

    await vi.waitFor(
      () => {
        expect(launches()).toBe(2);
      },
      { timeout: WAIT_MS },
    );
    await waitForStatus(supervisor, 'ready');

    expect(reports.join('\n')).toContain('window destroyed');
  });

  it('stops while the service is still starting, without calling it a failure', async () => {
    const { supervisor, reports } = supervise([STARTS_SLOWLY]);

    await supervisor.stop();
    await new Promise((resolve) => setTimeout(resolve, 100));

    expect(reports).toEqual([]);
    expect(supervisor.state()).toEqual({ status: 'starting' });
  });

  it('starts the service again on retry after a failure', async () => {
    const { supervisor, launches } = supervise([FAILS_AT_STARTUP, ANNOUNCES_AND_WAITS]);
    await waitForStatus(supervisor, 'failed');

    supervisor.retry();

    expect(supervisor.state()).toEqual({ status: 'starting' });
    await waitForStatus(supervisor, 'ready');
    expect(launches()).toBe(2);
  });

  it('ignores retry unless it failed', async () => {
    const { supervisor, launches } = supervise([ANNOUNCES_AND_WAITS]);
    await waitForStatus(supervisor, 'ready');

    supervisor.retry();

    expect(supervisor.state().status).toBe('ready');
    expect(launches()).toBe(1);
  });

  it('never restarts the service once stopped', async () => {
    const { supervisor, launches } = supervise([ANNOUNCES_THEN_DIES], { restartDelaysMs: [200] });
    await waitForStatus(supervisor, 'restarting');

    await supervisor.stop();
    await new Promise((resolve) => setTimeout(resolve, 400));

    expect(launches()).toBe(1);
  });

  it('stops telling a listener that unsubscribed', async () => {
    const { supervisor } = supervise([ANNOUNCES_AND_WAITS]);
    const listener = vi.fn();
    supervisor.onChange(listener)();

    await waitForStatus(supervisor, 'ready');

    expect(listener).not.toHaveBeenCalled();
  });

  it('is ready at once for a service someone else runs', async () => {
    const connection = { url: 'http://127.0.0.1:5000', token: 'token' };
    const supervisor = superviseLocalService({ launch: () => attachToRunningLocalService(connection) });
    supervisors.push(supervisor);

    await waitForStatus(supervisor, 'ready');

    expect(supervisor.state()).toEqual({ status: 'ready', connection });
  });
});
