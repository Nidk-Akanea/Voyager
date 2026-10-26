import { describe, expect, it, vi } from 'vitest';

import { downloadUpdate } from './updates';

interface FakeUpdate {
  readonly TargetFullRelease: { readonly Version: string };
}

const NEWER: FakeUpdate = { TargetFullRelease: { Version: '0.3.0' } };

function fakeLog() {
  return { info: vi.fn<(message: string) => void>(), warn: vi.fn<(message: string) => void>() };
}

describe('downloadUpdate', () => {
  it('downloads the newer version and says when it installs', async () => {
    const log = fakeLog();
    const downloadUpdateAsync = vi.fn(() => Promise.resolve());

    const outcome = await downloadUpdate(
      { checkForUpdatesAsync: () => Promise.resolve(NEWER), downloadUpdateAsync },
      log,
    );

    expect(outcome).toBe('downloaded');
    expect(downloadUpdateAsync).toHaveBeenCalledWith(NEWER);
    expect(log.info).toHaveBeenLastCalledWith(expect.stringContaining('0.3.0 is downloaded'));
  });

  it('downloads nothing when the app is up to date', async () => {
    const downloadUpdateAsync = vi.fn(() => Promise.resolve());

    const outcome = await downloadUpdate(
      { checkForUpdatesAsync: () => Promise.resolve(null), downloadUpdateAsync },
      fakeLog(),
    );

    expect(outcome).toBe('none');
    expect(downloadUpdateAsync).not.toHaveBeenCalled();
  });

  it('reports a failed check, such as no network, instead of throwing', async () => {
    const log = fakeLog();

    const outcome = await downloadUpdate(
      {
        checkForUpdatesAsync: () => Promise.reject(new Error('no route to host')),
        downloadUpdateAsync: () => Promise.resolve(),
      },
      log,
    );

    expect(outcome).toBe('failed');
    expect(log.warn).toHaveBeenCalledWith(expect.stringContaining('no route to host'));
  });

  it('reports a failed download instead of throwing', async () => {
    const log = fakeLog();

    const outcome = await downloadUpdate(
      {
        checkForUpdatesAsync: () => Promise.resolve(NEWER),
        downloadUpdateAsync: () => Promise.reject(new Error('disk full')),
      },
      log,
    );

    expect(outcome).toBe('failed');
    expect(log.warn).toHaveBeenCalledWith(expect.stringContaining('disk full'));
  });
});
