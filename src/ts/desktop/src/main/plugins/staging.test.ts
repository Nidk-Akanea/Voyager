import { createHash } from 'node:crypto';
import { mkdtemp, readdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { discardStagedPackage, isStagedName, stagePackage } from './staging';

const CONTENT = 'the bytes of a package';

let folder: string;
let staging: string;
let source: string;

beforeEach(async () => {
  folder = await mkdtemp(join(tmpdir(), 'voyager-staging-'));
  staging = join(folder, 'staging');
  source = join(folder, 'Trip of Traveler A.voyagerplugin');
  await writeFile(source, CONTENT);
});

afterEach(async () => {
  await rm(folder, { recursive: true, force: true });
});

describe('stagePackage', () => {
  it('copies the package under a random name, never the name the user chose', async () => {
    const staged = await stagePackage(source, staging);

    expect(isStagedName(staged.stagedName)).toBe(true);
    expect(await readdir(staging)).toEqual([staged.stagedName]);
    expect(await readFile(join(staging, staged.stagedName), 'utf8')).toBe(CONTENT);
  });

  it('gives the fingerprint and the size of the copy', async () => {
    const staged = await stagePackage(source, staging);

    expect(staged.sha256).toBe(createHash('sha256').update(CONTENT).digest('hex'));
    expect(staged.size).toBe(CONTENT.length);
  });

  it('makes a copy of its own each time', async () => {
    const first = await stagePackage(source, staging);
    const second = await stagePackage(source, staging);

    expect(first.stagedName).not.toBe(second.stagedName);
  });

  it('leaves nothing behind when the package cannot be read', async () => {
    await expect(stagePackage(join(folder, 'gone.voyagerplugin'), staging)).rejects.toThrow();

    expect(await readdir(staging)).toEqual([]);
  });
});

describe('discardStagedPackage', () => {
  it('deletes a prepared copy', async () => {
    const staged = await stagePackage(source, staging);

    await discardStagedPackage(staging, staged.stagedName);

    expect(await readdir(staging)).toEqual([]);
  });

  it('does nothing for a copy already gone', async () => {
    await expect(discardStagedPackage(staging, `${'a'.repeat(32)}.voyagerplugin`)).resolves.toBeUndefined();
  });

  it('never deletes a file whose name is not that of a prepared copy', async () => {
    await discardStagedPackage(folder, 'Trip of Traveler A.voyagerplugin');
    await discardStagedPackage(staging, '../Trip of Traveler A.voyagerplugin');

    expect(await readFile(source, 'utf8')).toBe(CONTENT);
  });
});

describe('isStagedName', () => {
  it.each([
    ['upper-case hexadecimal', `${'A'.repeat(32)}.voyagerplugin`],
    ['a short name', `${'a'.repeat(31)}.voyagerplugin`],
    ['another extension', `${'a'.repeat(32)}.zip`],
    ['a path', `../${'a'.repeat(32)}.voyagerplugin`],
  ])('refuses %s', (_case, name) => {
    expect(isStagedName(name)).toBe(false);
  });
});
