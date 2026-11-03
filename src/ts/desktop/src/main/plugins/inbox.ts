// The inbox of plugin packages (ADR-0016, decision 3): a folder of the user's data where packages wait, dropped there
// by hand or copied by "Add a package…". The page names a package by its file name, which is checked here as a name
// and never taken as a path.
import { randomBytes } from 'node:crypto';
import { constants } from 'node:fs';
import { copyFile, lstat, mkdir, open, readdir, rm } from 'node:fs/promises';
import { basename, dirname, extname, join, resolve } from 'node:path';

import type { AddPackageRefusal, InboxPackage } from '@voyager/grpc-client';

import { cleanDisplayText } from './displayText';
import { errorCode, hasRoomFor, isMissing, renameWithRetries } from './files';

/** The extension of a plugin package. Must match PluginPackages.EXTENSION in src/dotnet/Voyager.PluginHost. */
export const PACKAGE_EXTENSION = '.voyagerplugin';

// The longest file name Windows accepts.
const MAXIMUM_NAME_LENGTH = 255;
// What a name of the inbox can never hold: a separator, a stream, a character Windows forbids, a control character.
// eslint-disable-next-line no-control-regex -- control characters are exactly what the name must not hold
const FORBIDDEN_NAME_CHARACTERS = /[/\\:*?"<>|\u0000-\u001f]/u;
// How many "name (n)" are tried before giving up on a free name.
const MAXIMUM_NAME_ATTEMPTS = 100;
// The hidden name of a copy on its way into the inbox.
const ADDING_PREFIX = '.adding-';

/** Why a package could not be added to the inbox. */
export class InboxRefusalError extends Error {
  constructor(readonly reason: AddPackageRefusal) {
    super(`The package cannot be added to the inbox: ${reason}.`);
    this.name = 'InboxRefusalError';
  }
}

function hasPackageExtension(name: string): boolean {
  return extname(name).toLowerCase() === PACKAGE_EXTENSION;
}

/** Whether a text is the name of a package file of the inbox: a plain name with the extension, never a path. */
export function isInboxPackageName(name: unknown): name is string {
  return (
    typeof name === 'string' &&
    name.length > PACKAGE_EXTENSION.length &&
    name.length <= MAXIMUM_NAME_LENGTH &&
    !name.startsWith('.') &&
    !FORBIDDEN_NAME_CHARACTERS.test(name) &&
    basename(name) === name &&
    hasPackageExtension(name)
  );
}

/** The packages of the inbox, by name; a missing inbox is an empty one, and other files and links are left out. */
export async function listInbox(inbox: string): Promise<InboxPackage[]> {
  let entries;
  try {
    entries = await readdir(inbox, { withFileTypes: true });
  } catch (error) {
    if (isMissing(error)) {
      return [];
    }
    throw error;
  }
  const packages: InboxPackage[] = [];
  for (const entry of entries) {
    // Dirent describes the entry itself: a link is never a file here.
    if (!entry.isFile() || !isInboxPackageName(entry.name)) {
      continue;
    }
    try {
      const file = await lstat(join(inbox, entry.name));
      packages.push({
        name: entry.name,
        displayName: cleanDisplayText(entry.name),
        size: file.size,
        modifiedAt: file.mtimeMs,
      });
    } catch (error) {
      // Gone since the folder was read.
      if (!isMissing(error)) {
        throw error;
      }
    }
  }
  return packages.sort((first, second) => first.name.localeCompare(second.name));
}

/** The path of a package of the inbox; undefined when the name is not one, or no such file is there. */
export async function findInboxPackage(inbox: string, name: unknown): Promise<string | undefined> {
  if (!isInboxPackageName(name)) {
    return undefined;
  }
  const path = join(inbox, name);
  try {
    const file = await lstat(path);
    return file.isFile() ? path : undefined;
  } catch (error) {
    if (isMissing(error)) {
      return undefined;
    }
    throw error;
  }
}

// "name.voyagerplugin", then "name (2).voyagerplugin", "name (3).voyagerplugin"…
function candidateName(name: string, attempt: number): string {
  if (attempt === 1) {
    return name;
  }
  const stem = name.slice(0, -PACKAGE_EXTENSION.length);
  return `${stem} (${String(attempt)})${name.slice(-PACKAGE_EXTENSION.length)}`;
}

// A copy on its way into the inbox: a hidden name, which listInbox never shows as a package.
async function hiddenCopy(source: string, inbox: string): Promise<string> {
  const temporary = join(inbox, `${ADDING_PREFIX}${randomBytes(8).toString('hex')}.tmp`);
  try {
    await copyFile(source, temporary, constants.COPYFILE_EXCL);
    return temporary;
  } catch (error) {
    // Only the hidden copy is deleted: never a file this function did not create.
    await rm(temporary, { force: true });
    throw errorCode(error) === 'ENOSPC' ? new InboxRefusalError('not-enough-space') : error;
  }
}

// Takes the first free name: an empty file created only if none is there, which the copy then replaces.
async function reserveName(name: string, inbox: string): Promise<string> {
  for (let attempt = 1; attempt <= MAXIMUM_NAME_ATTEMPTS; attempt++) {
    const target = candidateName(name, attempt);
    if (!isInboxPackageName(target)) {
      break;
    }
    try {
      await (await open(join(inbox, target), 'wx')).close();
      return target;
    } catch (error) {
      if (errorCode(error) !== 'EEXIST') {
        throw error;
      }
    }
  }
  throw new Error('No free name is left in the inbox for this package.');
}

// What an addition cut short (the app closed during the copy) left: there is only one app, so nothing is on its way.
async function deleteLeftovers(inbox: string): Promise<void> {
  for (const entry of await readdir(inbox)) {
    if (entry.startsWith(ADDING_PREFIX)) {
      await rm(join(inbox, entry), { force: true });
    }
  }
}

/**
 * Copies a package file the user chose into the inbox, under its own name or "name (2)" when it is taken. A package
 * already there is never replaced, and a copy cut short never shows as a package.
 * @param source The file, as the native dialog of the main process gave it: never a path from the page.
 * @returns The name of the package in the inbox.
 * @throws InboxRefusalError when the file is not a package, is empty, or the disk has no room for it.
 */
export async function addToInbox(source: string, inbox: string): Promise<string> {
  const name = basename(source);
  if (!isInboxPackageName(name)) {
    throw new InboxRefusalError('not-a-package');
  }
  const file = await lstat(source);
  if (!file.isFile()) {
    throw new InboxRefusalError('not-a-package');
  }
  if (file.size === 0) {
    throw new InboxRefusalError('empty');
  }
  // Chosen in the inbox itself: it is there already.
  if (resolve(dirname(source)).toLowerCase() === resolve(inbox).toLowerCase()) {
    return name;
  }
  await mkdir(inbox, { recursive: true });
  await deleteLeftovers(inbox);
  if (!(await hasRoomFor(inbox, file.size))) {
    throw new InboxRefusalError('not-enough-space');
  }
  const copy = await hiddenCopy(source, inbox);
  try {
    const target = await reserveName(name, inbox);
    try {
      await renameWithRetries(copy, join(inbox, target));
    } catch (error) {
      // The reserved name is empty: it is not a package.
      await rm(join(inbox, target), { force: true });
      throw error;
    }
    return target;
  } finally {
    await rm(copy, { force: true });
  }
}
