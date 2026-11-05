import { describe, expect, it, vi } from 'vitest';

import { servedPluginKey, type ServedPlugins } from './servedPlugins';
import { createServedPluginsReader } from './servedPluginsReader';

const CLOCK = { origin: 'shipped', pluginId: 'diagnostics-clock', version: '1.2.0' } as const;
const SERVED: ServedPlugins = new Map([[servedPluginKey('shipped', 'diagnostics-clock', '1.2.0'), 'folder']]);

/** A reading the test settles itself. */
function deferred() {
  let resolve: (value: ServedPlugins | undefined) => void = () => undefined;
  const promise = new Promise<ServedPlugins | undefined>((settle) => {
    resolve = settle;
  });
  return { promise, resolve };
}

describe('createServedPluginsReader', () => {
  it('reads as the view opens, so that the page finds its plugins without waiting for the server again', async () => {
    const read = vi.fn(() => Promise.resolve<ServedPlugins | undefined>(SERVED));
    const reader = createServedPluginsReader({ read, retryAfterMs: 5_000 });

    reader.refresh();

    expect(await reader.find(CLOCK)).toBe(SERVED);
    expect(read).toHaveBeenCalledOnce();
  });

  it('serves a known plugin at once, even while another reading is under way', async () => {
    let time = 0;
    const pending = deferred();
    const read = vi.fn().mockResolvedValueOnce(SERVED).mockReturnValueOnce(pending.promise);
    const reader = createServedPluginsReader({ read, retryAfterMs: 5_000, now: () => time });
    await reader.find(CLOCK);
    time = 10_000;

    void reader.find({ ...CLOCK, version: '1.3.0' });

    expect(await reader.find(CLOCK)).toBe(SERVED);
    pending.resolve(SERVED);
  });

  it('reads again for a version not served yet, once at a time, and not sooner than allowed', async () => {
    let time = 0;
    const pending = deferred();
    const read = vi.fn().mockResolvedValueOnce(new Map()).mockReturnValueOnce(pending.promise);
    const reader = createServedPluginsReader({ read, retryAfterMs: 5_000, now: () => time });
    await reader.find(CLOCK);

    time = 1_000;
    await reader.find(CLOCK);
    expect(read).toHaveBeenCalledOnce();

    time = 6_000;
    const first = reader.find(CLOCK);
    const second = reader.find(CLOCK);
    pending.resolve(SERVED);

    expect(await first).toBe(SERVED);
    expect(await second).toBe(SERVED);
    expect(read).toHaveBeenCalledTimes(2);
  });

  it('keeps what it served when the server cannot tell', async () => {
    let time = 0;
    const read = vi.fn().mockResolvedValueOnce(SERVED).mockResolvedValueOnce(undefined);
    const reader = createServedPluginsReader({ read, retryAfterMs: 5_000, now: () => time });
    await reader.find(CLOCK);
    time = 10_000;

    expect(await reader.find({ ...CLOCK, version: '1.3.0' })).toBe(SERVED);
  });
});
