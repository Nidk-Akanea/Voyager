import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { errorCode, hasRoomFor, isMissing, renameWithRetries } from './files';

let folder: string;

beforeEach(async () => {
  folder = await mkdtemp(join(tmpdir(), 'voyager-files-'));
});

afterEach(async () => {
  vi.restoreAllMocks();
  await rm(folder, { recursive: true, force: true });
});

function failure(code: string): Error {
  return Object.assign(new Error(`${code}: C:\\Users\\Traveler A`), { code });
}

describe('renameWithRetries', () => {
  it('replaces the target', async () => {
    await writeFile(join(folder, 'from'), 'new');
    await writeFile(join(folder, 'to'), 'old');

    await renameWithRetries(join(folder, 'from'), join(folder, 'to'));

    expect(await readFile(join(folder, 'to'), 'utf8')).toBe('new');
  });

  it('gives up at once on a failure that waiting does not cure', async () => {
    await expect(renameWithRetries(join(folder, 'missing'), join(folder, 'to'), [1, 1])).rejects.toMatchObject({
      code: 'ENOENT',
    });
  });
});

describe('errorCode and isMissing', () => {
  it('read the code of a failure of the file system', () => {
    expect(errorCode(failure('EBUSY'))).toBe('EBUSY');
    expect(isMissing(failure('ENOENT'))).toBe(true);
    expect(isMissing(failure('ENOTDIR'))).toBe(true);
    expect(isMissing(failure('EBUSY'))).toBe(false);
  });

  it('find no code in anything else', () => {
    expect(errorCode(new Error('no code'))).toBeUndefined();
    expect(errorCode('ENOENT')).toBeUndefined();
  });
});

describe('hasRoomFor', () => {
  it('has room for a small file, and none for one larger than any disk', async () => {
    expect(await hasRoomFor(folder, 1)).toBe(true);
    expect(await hasRoomFor(folder, Number.MAX_SAFE_INTEGER)).toBe(false);
  });
});
