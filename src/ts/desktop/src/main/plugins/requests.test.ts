import { mkdtemp, readdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import {
  cancelRequest,
  deleteTemporaryRequestFiles,
  describeRequest,
  isPluginTarget,
  newRequest,
  pendingRequests,
  placeRequest,
  readAppliedRequestIds,
  readRequests,
  writeRequests,
  type PluginRequestEntry,
} from './requests';

const CLOCK = { origin: 'shipped', pluginId: 'diagnostics-clock' } as const;
const TOOL = { origin: 'installed', pluginId: 'tool' } as const;
const SHA256 = 'a'.repeat(64);

function install(stagedName: string): PluginRequestEntry {
  return { ...newRequest('install', TOOL), package: stagedName, sha256: SHA256, packageName: 'tool.voyagerplugin' };
}

const COPY_V2 = `${'2'.repeat(32)}.voyagerplugin`;
const COPY_V3 = `${'3'.repeat(32)}.voyagerplugin`;

let folder: string;
let file: string;

beforeEach(async () => {
  folder = await mkdtemp(join(tmpdir(), 'voyager-requests-'));
  file = join(folder, 'requests.json');
});

afterEach(async () => {
  await rm(folder, { recursive: true, force: true });
});

describe('placeRequest', () => {
  it('adds a request for a plugin that has none', () => {
    const disable = newRequest('disable', CLOCK);

    expect(placeRequest([], disable, false)).toEqual({ requests: [disable], discardedPackages: [] });
  });

  it('cancels both when enabling a plugin whose disabling waits: it is enabled already', () => {
    const change = placeRequest([newRequest('disable', CLOCK)], newRequest('enable', CLOCK), true);

    expect(change.requests).toEqual([]);
  });

  it('cancels the installation of a new plugin when it is uninstalled, and deletes its copy', () => {
    const change = placeRequest([install(COPY_V2)], newRequest('uninstall', TOOL), true);

    expect(change).toEqual({ requests: [], discardedPackages: [COPY_V2] });
  });

  it('keeps the uninstallation of a plugin already installed that was about to be replaced', () => {
    const uninstall = newRequest('uninstall', TOOL);

    const change = placeRequest([install(COPY_V2)], uninstall, false);

    expect(change).toEqual({ requests: [uninstall], discardedPackages: [COPY_V2] });
  });

  it('turns an uninstallation followed by an installation into a replacement', () => {
    const replacement = install(COPY_V2);

    expect(placeRequest([newRequest('uninstall', TOOL)], replacement, false).requests).toEqual([replacement]);
  });

  it('keeps the second of two installations, and deletes the copy of the first', () => {
    const v3 = install(COPY_V3);

    expect(placeRequest([install(COPY_V2)], v3, false)).toEqual({ requests: [v3], discardedPackages: [COPY_V2] });
  });

  it('never touches the requests of other plugins', () => {
    const disableClock = newRequest('disable', CLOCK);
    const sameIdOtherOrigin = newRequest('disable', { origin: 'installed', pluginId: 'diagnostics-clock' });

    const change = placeRequest([disableClock, sameIdOtherOrigin], install(COPY_V2), false);

    expect(change.requests.slice(0, 2)).toEqual([disableClock, sameIdOtherOrigin]);
  });
});

describe('cancelRequest', () => {
  it('removes the request waiting for a plugin, and deletes its copy', () => {
    const disableClock = newRequest('disable', CLOCK);

    expect(cancelRequest([disableClock, install(COPY_V2)], TOOL)).toEqual({
      requests: [disableClock],
      discardedPackages: [COPY_V2],
    });
  });

  it('changes nothing when no request waits for the plugin', () => {
    const disableClock = newRequest('disable', CLOCK);

    expect(cancelRequest([disableClock], TOOL).requests).toEqual([disableClock]);
  });
});

describe('newRequest', () => {
  it('gives each request an identifier of its own, in the form the service accepts', () => {
    const first = newRequest('disable', CLOCK);
    const second = newRequest('disable', CLOCK);

    expect(first.id).not.toBe(second.id);
    expect(first.id).toMatch(/^[A-Za-z0-9-]{1,64}$/u);
  });
});

describe('writeRequests and readRequests', () => {
  it('reads back what was written', async () => {
    const requests = [newRequest('disable', CLOCK), install(COPY_V2)];

    await writeRequests(file, requests);

    expect(await readRequests(file)).toEqual({ requests, ignored: 0 });
  });

  it('writes the format the service reads', async () => {
    const disable = newRequest('disable', CLOCK);

    await writeRequests(file, [disable]);

    expect(JSON.parse(await readFile(file, 'utf8'))).toEqual({
      requests: [{ id: disable.id, action: 'disable', origin: 'shipped', pluginId: 'diagnostics-clock' }],
    });
  });

  it('leaves no temporary file behind', async () => {
    await writeRequests(file, [newRequest('disable', CLOCK)]);
    await writeRequests(file, []);

    expect(await readdir(folder)).toEqual(['requests.json']);
  });

  it('reads no request when the file does not exist', async () => {
    expect(await readRequests(file)).toEqual({ requests: [], ignored: 0 });
  });

  it('drops an entry that is not valid, and keeps the others', async () => {
    const disable = newRequest('disable', CLOCK);
    await writeFile(
      file,
      JSON.stringify({
        requests: [
          disable,
          { id: 'r2', action: 'explode', origin: 'shipped', pluginId: 'diagnostics-clock' },
          { id: 'r3', action: 'disable', origin: 'shipped', pluginId: '../outside' },
          { id: 'r4', action: 'install', origin: 'installed', pluginId: 'tool', package: 'C:\\file.voyagerplugin' },
          'not an object',
        ],
      }),
    );

    expect(await readRequests(file)).toEqual({ requests: [disable], ignored: 4 });
  });

  it('counts a damaged file as one entry dropped, so that the next write replaces it', async () => {
    await writeFile(file, '{ "requests": [ cut short');

    expect(await readRequests(file)).toEqual({ requests: [], ignored: 1 });
  });
});

describe('the file the service reads', () => {
  // Read by PluginStoreFilesTests in tests/Voyager.PluginHost.Tests: both sides agree on this very file.
  const FIXTURE = join(
    import.meta.dirname,
    '..',
    '..',
    '..',
    '..',
    '..',
    '..',
    'tests',
    'fixtures',
    'plugin-requests.json',
  );

  it('is written exactly as the fixture the .NET tests read', async () => {
    const fixture = await readFile(FIXTURE, 'utf8');
    const { requests } = await readRequests(FIXTURE);

    await writeRequests(file, requests);

    expect(requests).toHaveLength(4);
    expect(await readFile(file, 'utf8')).toBe(fixture.replaceAll('\r\n', '\n'));
  });
});

describe('deleteTemporaryRequestFiles', () => {
  it('deletes what a write cut short left, and nothing else', async () => {
    await writeFile(file, '{ "requests": [] }');
    await writeFile(`${file}.0123456789abcdef.tmp`, 'cut short');
    await writeFile(join(folder, 'state.json.0123456789abcdef.tmp'), 'not ours');

    await deleteTemporaryRequestFiles(file);

    expect((await readdir(folder)).sort()).toEqual(['requests.json', 'state.json.0123456789abcdef.tmp']);
  });

  it('does nothing when the folder does not exist yet', async () => {
    await expect(deleteTemporaryRequestFiles(join(folder, 'missing', 'requests.json'))).resolves.toBeUndefined();
  });
});

describe('readAppliedRequestIds', () => {
  const stateFile = () => join(folder, 'state.json');

  it('reads the requests the service applied', async () => {
    await writeFile(stateFile(), JSON.stringify({ plugins: [], appliedRequests: ['r1', 'r2', 3] }));

    expect(await readAppliedRequestIds(stateFile())).toEqual(new Set(['r1', 'r2']));
  });

  it('reads none from a state that is missing or damaged: every request is then still pending', async () => {
    expect(await readAppliedRequestIds(stateFile())).toEqual(new Set());

    await writeFile(stateFile(), 'not JSON');

    expect(await readAppliedRequestIds(stateFile())).toEqual(new Set());
  });
});

describe('pendingRequests', () => {
  it('leaves out the requests the service applied', () => {
    const applied = newRequest('disable', CLOCK);
    const pending = install(COPY_V2);

    expect(pendingRequests([applied, pending], new Set([applied.id]))).toEqual([pending]);
  });
});

describe('describeRequest', () => {
  it('tells the page neither the prepared copy nor the fingerprint', () => {
    expect(describeRequest(install(COPY_V2))).toEqual({
      action: 'install',
      origin: 'installed',
      pluginId: 'tool',
      packageName: 'tool.voyagerplugin',
    });
  });
});

describe('isPluginTarget', () => {
  it('accepts an origin and an identifier', () => {
    expect(isPluginTarget(CLOCK)).toBe(true);
  });

  it.each([
    ['an unknown origin', { origin: 'group', pluginId: 'tool' }],
    ['a path as identifier', { origin: 'installed', pluginId: '../tool' }],
    ['an identifier with upper case', { origin: 'installed', pluginId: 'Tool' }],
    ['an identifier too long', { origin: 'installed', pluginId: 'a'.repeat(65) }],
    ['the name of a Windows device', { origin: 'installed', pluginId: 'com1' }],
    ['a text', 'installed/tool'],
    ['nothing', null],
  ])('refuses %s', (_case, value) => {
    expect(isPluginTarget(value)).toBe(false);
  });
});
