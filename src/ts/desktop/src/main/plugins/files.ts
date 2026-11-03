// What the plugin management needs of the file system beyond Node.js: room on the disk, and a replacement that
// waits for an antivirus or the indexer to let go of a file it just opened, as Windows often does.
import { rename, statfs } from 'node:fs/promises';
import { setTimeout as delay } from 'node:timers/promises';

// Room left on the disk beyond a copy, so that a package never fills it: the database and the logs still need it.
const SPARE_BYTES = 64 * 1024 * 1024;
// The waits before each new attempt; the same as MoveFolder in src/dotnet/Voyager.PluginHost/PluginStore.cs.
const RETRY_DELAYS_MS: readonly number[] = [100, 250, 500];
// What Windows answers while another program holds the file.
const BUSY_CODES: readonly string[] = ['EPERM', 'EACCES', 'EBUSY'];

/** The code of a failure of the file system (ENOENT, ENOSPC…); undefined for anything else. */
export function errorCode(error: unknown): string | undefined {
  if (error instanceof Error && 'code' in error && typeof error.code === 'string') {
    return error.code;
  }
  return undefined;
}

/** Whether a failure says that the file, or a folder on its way, is not there. */
export function isMissing(error: unknown): boolean {
  const code = errorCode(error);
  return code === 'ENOENT' || code === 'ENOTDIR';
}

/** Whether the disk of a folder has room for a file of this size, and some to spare. */
export async function hasRoomFor(folder: string, bytes: number): Promise<boolean> {
  const disk = await statfs(folder);
  return disk.bavail * disk.bsize >= bytes + SPARE_BYTES;
}

/** Renames a file over another, trying again a few times while another program holds one of them. */
export async function renameWithRetries(from: string, to: string, delaysMs = RETRY_DELAYS_MS): Promise<void> {
  for (let attempt = 0; ; attempt++) {
    try {
      await rename(from, to);
      return;
    } catch (error) {
      const wait = delaysMs[attempt];
      if (wait === undefined || !BUSY_CODES.includes(errorCode(error) ?? '')) {
        throw error;
      }
      await delay(wait);
    }
  }
}
