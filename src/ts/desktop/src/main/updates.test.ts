import { describe, expect, it, vi } from 'vitest';

import { updateApp, type UpdateEvent, type UpdateClient } from './updates';

interface FakeUpdate {
  readonly TargetFullRelease: { readonly Version: string };
}

const NEWER: FakeUpdate = { TargetFullRelease: { Version: '0.3.0' } };

function fakeLog() {
  return { info: vi.fn<(message: string) => void>(), warn: vi.fn<(message: string) => void>() };
}

/** An update server with a newer version, unless told otherwise. */
function fakeSource(overrides: Partial<UpdateClient<FakeUpdate>> = {}): UpdateClient<FakeUpdate> {
  return {
    checkForUpdatesAsync: () => Promise.resolve(NEWER),
    downloadUpdateAsync: (_update, progress) => {
      progress?.(40);
      progress?.(100);
      return Promise.resolve();
    },
    waitExitThenApplyUpdate: vi.fn(),
    ...overrides,
  };
}

describe('updateApp', () => {
  it('downloads the newer version, then hands it to the updater to install and restart', async () => {
    const source = fakeSource();
    const events: UpdateEvent[] = [];

    const outcome = await updateApp(source, fakeLog(), (event) => events.push(event));

    expect(outcome).toBe('restarting');
    expect(events).toEqual([
      { kind: 'checking' },
      { kind: 'downloading', version: '0.3.0', percent: 0 },
      { kind: 'downloading', version: '0.3.0', percent: 40 },
      { kind: 'downloading', version: '0.3.0', percent: 100 },
      { kind: 'installing', version: '0.3.0' },
    ]);
    expect(source.waitExitThenApplyUpdate).toHaveBeenCalledWith(NEWER, true, true);
  });

  it('keeps the percentage between 0 and 100', async () => {
    const source = fakeSource({
      downloadUpdateAsync: (_update, progress) => {
        progress?.(-5);
        progress?.(100.4);
        progress?.(12.6);
        return Promise.resolve();
      },
    });
    const events: UpdateEvent[] = [];

    await updateApp(source, fakeLog(), (event) => events.push(event));

    const percents = events.flatMap((event) => (event.kind === 'downloading' ? [event.percent] : []));
    expect(percents).toEqual([0, 100, 13]);
  });

  it('installs nothing when the app is up to date', async () => {
    const source = fakeSource({ checkForUpdatesAsync: () => Promise.resolve(null) });

    const outcome = await updateApp(source, fakeLog(), () => undefined);

    expect(outcome).toBe('up-to-date');
    expect(source.waitExitThenApplyUpdate).not.toHaveBeenCalled();
  });

  it('reports a failed check, such as no network, instead of throwing', async () => {
    const log = fakeLog();
    const source = fakeSource({ checkForUpdatesAsync: () => Promise.reject(new Error('no route to host')) });

    const outcome = await updateApp(source, log, () => undefined);

    expect(outcome).toBe('failed');
    expect(log.warn).toHaveBeenCalledWith(expect.stringContaining('no route to host'));
    expect(source.waitExitThenApplyUpdate).not.toHaveBeenCalled();
  });

  it('gives up on a server that does not answer', async () => {
    const log = fakeLog();
    const source = fakeSource({ checkForUpdatesAsync: () => new Promise(() => undefined) });

    const outcome = await updateApp(source, log, () => undefined, { checkMs: 50 });

    expect(outcome).toBe('failed');
    expect(log.warn).toHaveBeenCalledWith(expect.stringContaining('did not answer'));
  });

  it('reports a failed download instead of throwing, and does not restart', async () => {
    const log = fakeLog();
    const source = fakeSource({ downloadUpdateAsync: () => Promise.reject(new Error('disk full')) });

    const outcome = await updateApp(source, log, () => undefined);

    expect(outcome).toBe('failed');
    expect(log.warn).toHaveBeenCalledWith(expect.stringContaining('disk full'));
    expect(source.waitExitThenApplyUpdate).not.toHaveBeenCalled();
  });

  it('gives up on a download whose progress stops moving, and does not restart', async () => {
    const log = fakeLog();
    const source = fakeSource({
      downloadUpdateAsync: (_update, progress) => {
        progress?.(10);
        return new Promise(() => undefined);
      },
    });

    const outcome = await updateApp(source, log, () => undefined, { downloadIdleMs: 50 });

    expect(outcome).toBe('failed');
    expect(log.warn).toHaveBeenCalledWith(expect.stringContaining('no progress'));
    expect(source.waitExitThenApplyUpdate).not.toHaveBeenCalled();
  });

  it('lets a slow download go on as long as its progress moves', async () => {
    const source = fakeSource({
      downloadUpdateAsync: async (_update, progress) => {
        for (const percent of [20, 40, 60, 80]) {
          await new Promise((resolve) => setTimeout(resolve, 30));
          progress?.(percent);
        }
      },
    });

    const outcome = await updateApp(source, fakeLog(), () => undefined, { downloadIdleMs: 80 });

    expect(outcome).toBe('restarting');
  });

  it('ignores a percentage that arrives after the download is over', async () => {
    let lateProgress: ((percent: number) => void) | undefined;
    const source = fakeSource({
      downloadUpdateAsync: (_update, progress) => {
        lateProgress = progress;
        progress?.(100);
        return Promise.resolve();
      },
    });
    const events: UpdateEvent[] = [];

    await updateApp(source, fakeLog(), (event) => events.push(event));
    lateProgress?.(99);

    expect(events.at(-1)).toEqual({ kind: 'installing', version: '0.3.0' });
  });

  it('reports a failure of the updater itself instead of throwing', async () => {
    const log = fakeLog();
    const source = fakeSource({
      waitExitThenApplyUpdate: () => {
        throw new Error('updater not found');
      },
    });

    const outcome = await updateApp(source, log, () => undefined);

    expect(outcome).toBe('failed');
    expect(log.warn).toHaveBeenCalledWith(expect.stringContaining('updater not found'));
  });
});
