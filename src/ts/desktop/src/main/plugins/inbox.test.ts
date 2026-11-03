import { mkdir, mkdtemp, readdir, readFile, rm, symlink, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { addToInbox, findInboxPackage, InboxRefusalError, isInboxPackageName, listInbox } from './inbox';

let folder: string;
let inbox: string;

beforeEach(async () => {
  folder = await mkdtemp(join(tmpdir(), 'voyager-inbox-'));
  inbox = join(folder, 'inbox');
});

afterEach(async () => {
  await rm(folder, { recursive: true, force: true });
});

describe('isInboxPackageName', () => {
  it.each(['clock.voyagerplugin', 'Clock 1.2.voyagerplugin', 'horloge-été.VOYAGERPLUGIN'])('accepts %s', (name) => {
    expect(isInboxPackageName(name)).toBe(true);
  });

  it.each([
    ['a path that leaves the inbox', '../clock.voyagerplugin'],
    ['a path with a backslash', 'sub\\clock.voyagerplugin'],
    ['a path with a slash', 'sub/clock.voyagerplugin'],
    ['an absolute path', 'C:\\clock.voyagerplugin'],
    ['a stream', 'clock.voyagerplugin:hidden'],
    ['another extension', 'clock.zip'],
    ['the extension alone', '.voyagerplugin'],
    ['a hidden file', '.clock.voyagerplugin'],
    ['a control character', 'clock\n.voyagerplugin'],
    ['a name too long for Windows', `${'x'.repeat(250)}.voyagerplugin`],
    ['an empty text', ''],
  ])('refuses %s', (_case, name) => {
    expect(isInboxPackageName(name)).toBe(false);
  });

  it('refuses what is not a text', () => {
    expect(isInboxPackageName({ name: 'clock.voyagerplugin' })).toBe(false);
    expect(isInboxPackageName(undefined)).toBe(false);
  });
});

describe('listInbox', () => {
  it('is empty when the inbox does not exist yet', async () => {
    expect(await listInbox(inbox)).toEqual([]);
  });

  it('lists the packages by name, with their size, and leaves out the other files and the folders', async () => {
    await mkdir(join(inbox, 'folder.voyagerplugin'), { recursive: true });
    await writeFile(join(inbox, 'b.voyagerplugin'), 'bb');
    await writeFile(join(inbox, 'a.voyagerplugin'), 'a');
    await writeFile(join(inbox, 'notes.txt'), 'not a package');

    const packages = await listInbox(inbox);

    expect(packages.map(({ name, size }) => ({ name, size }))).toEqual([
      { name: 'a.voyagerplugin', size: 1 },
      { name: 'b.voyagerplugin', size: 2 },
    ]);
    expect(packages[0]?.modifiedAt).toBeGreaterThan(0);
  });

  it('gives a cleaned name to show', async () => {
    await mkdir(inbox);
    await writeFile(join(inbox, 'report\u202Etxt.voyagerplugin'), 'a');

    const [entry] = await listInbox(inbox);

    expect(entry?.name).toBe('report\u202Etxt.voyagerplugin');
    expect(entry?.displayName).toBe('reporttxt.voyagerplugin');
  });

  it.skipIf(process.platform === 'win32')('leaves out a link, even to a package', async () => {
    await mkdir(inbox);
    await writeFile(join(folder, 'outside.voyagerplugin'), 'a');
    await symlink(join(folder, 'outside.voyagerplugin'), join(inbox, 'link.voyagerplugin'));

    expect(await listInbox(inbox)).toEqual([]);
  });
});

describe('findInboxPackage', () => {
  it('finds a package of the inbox by its name', async () => {
    await mkdir(inbox);
    await writeFile(join(inbox, 'clock.voyagerplugin'), 'a');

    expect(await findInboxPackage(inbox, 'clock.voyagerplugin')).toBe(join(inbox, 'clock.voyagerplugin'));
  });

  it('finds nothing for a package that is gone', async () => {
    expect(await findInboxPackage(inbox, 'clock.voyagerplugin')).toBeUndefined();
  });

  it('never follows a name out of the inbox', async () => {
    await mkdir(inbox);
    await writeFile(join(folder, 'outside.voyagerplugin'), 'a');

    expect(await findInboxPackage(inbox, '../outside.voyagerplugin')).toBeUndefined();
    expect(await findInboxPackage(inbox, join(folder, 'outside.voyagerplugin'))).toBeUndefined();
  });

  it('finds nothing for a folder named like a package', async () => {
    await mkdir(join(inbox, 'clock.voyagerplugin'), { recursive: true });

    expect(await findInboxPackage(inbox, 'clock.voyagerplugin')).toBeUndefined();
  });
});

describe('addToInbox', () => {
  it('copies the package into the inbox under its own name', async () => {
    const source = join(folder, 'clock.voyagerplugin');
    await writeFile(source, 'package');

    const name = await addToInbox(source, inbox);

    expect(name).toBe('clock.voyagerplugin');
    expect(await readFile(join(inbox, name), 'utf8')).toBe('package');
  });

  it('never replaces a package already in the inbox: the copy takes the next free name', async () => {
    const source = join(folder, 'clock.voyagerplugin');
    await writeFile(source, 'new');
    await mkdir(inbox);
    await writeFile(join(inbox, 'clock.voyagerplugin'), 'old');
    await writeFile(join(inbox, 'clock (2).voyagerplugin'), 'old too');

    const name = await addToInbox(source, inbox);

    expect(name).toBe('clock (3).voyagerplugin');
    expect(await readFile(join(inbox, 'clock.voyagerplugin'), 'utf8')).toBe('old');
    expect(await readFile(join(inbox, name), 'utf8')).toBe('new');
  });

  it('never deletes a package already in the inbox when the chosen file cannot be copied', async () => {
    await mkdir(inbox);
    await writeFile(join(inbox, 'clock.voyagerplugin'), 'already there');
    const source = join(folder, 'clock.voyagerplugin');
    await writeFile(source, 'new');
    await rm(source);

    await expect(addToInbox(source, inbox)).rejects.toThrow();

    expect(await readFile(join(inbox, 'clock.voyagerplugin'), 'utf8')).toBe('already there');
    expect(await readdir(inbox)).toEqual(['clock.voyagerplugin']);
  });

  it('deletes what an addition cut short left, and never shows it as a package', async () => {
    await mkdir(inbox);
    await writeFile(join(inbox, '.adding-0123456789abcdef.tmp'), 'cut short');
    const source = join(folder, 'clock.voyagerplugin');
    await writeFile(source, 'package');

    await addToInbox(source, inbox);

    expect(await readdir(inbox)).toEqual(['clock.voyagerplugin']);
  });

  it('keeps a package chosen in the inbox itself where it is', async () => {
    await mkdir(inbox);
    await writeFile(join(inbox, 'clock.voyagerplugin'), 'package');

    expect(await addToInbox(join(inbox, 'clock.voyagerplugin'), inbox)).toBe('clock.voyagerplugin');
    expect(await listInbox(inbox)).toHaveLength(1);
  });

  it('refuses a file without the extension of a package', async () => {
    const source = join(folder, 'clock.zip');
    await writeFile(source, 'package');

    await expect(addToInbox(source, inbox)).rejects.toEqual(new InboxRefusalError('not-a-package'));
  });

  it('refuses an empty file', async () => {
    const source = join(folder, 'clock.voyagerplugin');
    await writeFile(source, '');

    await expect(addToInbox(source, inbox)).rejects.toMatchObject({ reason: 'empty' });
  });

  it('refuses a folder named like a package', async () => {
    const source = join(folder, 'clock.voyagerplugin');
    await mkdir(source);

    await expect(addToInbox(source, inbox)).rejects.toMatchObject({ reason: 'not-a-package' });
  });
});
