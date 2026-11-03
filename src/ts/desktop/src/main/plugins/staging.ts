// The prepared copies of the packages the user chose to install (ADR-0016, decision 3): private copies that the
// local service examines and then installs, so that the file of the inbox may change or go without effect. Each one
// has a random name, never the name of the file the user chose, which could be personal.
import { createHash, randomBytes } from 'node:crypto';
import { constants, createReadStream } from 'node:fs';
import { copyFile, lstat, mkdir, rm } from 'node:fs/promises';
import { join } from 'node:path';
import { pipeline } from 'node:stream/promises';

import { errorCode, hasRoomFor } from './files';
import { PACKAGE_EXTENSION } from './inbox';

// Must match PluginPackages.IsValidStagedName in src/dotnet/Voyager.PluginHost/Packages/PluginPackages.cs.
const STAGED_NAME_PATTERN = /^[0-9a-f]{32}\.voyagerplugin$/u;
const STAGED_NAME_BYTES = 16;

/** A prepared copy, with the fingerprint the confirmation shows and the service checks again before installing. */
export interface StagedPackage {
  /** The name of the copy in the staging folder. */
  readonly stagedName: string;
  /** The SHA-256 fingerprint of the copy, in lower-case hexadecimal. */
  readonly sha256: string;
  /** The size of the copy, in bytes. */
  readonly size: number;
}

/** The disk has no room for the copy: the package is refused, as the service would refuse to extract it. */
export class NotEnoughSpaceError extends Error {
  constructor() {
    super('The disk has no room for a prepared copy of the package.');
    this.name = 'NotEnoughSpaceError';
  }
}

/** Whether a text is the name of a prepared copy: 32 lower-case hexadecimal characters and the extension. */
export function isStagedName(name: unknown): name is string {
  return typeof name === 'string' && STAGED_NAME_PATTERN.test(name);
}

async function sha256Of(file: string): Promise<string> {
  const hash = createHash('sha256');
  await pipeline(createReadStream(file), hash);
  return hash.digest('hex');
}

/**
 * Copies a package into the staging folder under a random name, and computes the fingerprint of the copy: the copy,
 * not the file of the inbox, is what the service examines and installs.
 * @throws NotEnoughSpaceError when the disk has no room for the copy: nothing is left behind.
 */
export async function stagePackage(source: string, staging: string): Promise<StagedPackage> {
  await mkdir(staging, { recursive: true });
  // Checked before copying: filling the disk would make the database and the logs fail too.
  if (!(await hasRoomFor(staging, (await lstat(source)).size))) {
    throw new NotEnoughSpaceError();
  }
  const stagedName = `${randomBytes(STAGED_NAME_BYTES).toString('hex')}${PACKAGE_EXTENSION}`;
  const copy = join(staging, stagedName);
  try {
    await copyFile(source, copy, constants.COPYFILE_EXCL);
    const [sha256, file] = await Promise.all([sha256Of(copy), lstat(copy)]);
    return { stagedName, sha256, size: file.size };
  } catch (error) {
    await rm(copy, { force: true });
    throw errorCode(error) === 'ENOSPC' ? new NotEnoughSpaceError() : error;
  }
}

/** Deletes a prepared copy that no request names any more; a name that is not one of a copy is ignored. */
export async function discardStagedPackage(staging: string, stagedName: string): Promise<void> {
  if (isStagedName(stagedName)) {
    await rm(join(staging, stagedName), { force: true });
  }
}
