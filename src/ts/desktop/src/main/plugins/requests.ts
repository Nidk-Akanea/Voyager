// The requests of the user (ADR-0016, decision 4): written by the main process alone, after the user confirmed them,
// and applied by the local service at its next start. The format is the one PluginStoreFiles reads in
// src/dotnet/Voyager.PluginHost/PluginStoreFiles.cs. Per plugin, the last request replaces the previous one.
import { randomBytes, randomUUID } from 'node:crypto';
import { mkdir, open, readdir, readFile, rm } from 'node:fs/promises';
import { basename, dirname, join } from 'node:path';

import type {
  PendingPluginRequest,
  PluginOriginName,
  PluginRequestActionName,
  PluginTarget,
} from '@voyager/grpc-client';

import { cleanDisplayText } from './displayText';
import { isMissing, renameWithRetries } from './files';
import { isStagedName } from './staging';

/** A request as the file holds it. */
export interface PluginRequestEntry extends PluginTarget {
  /** Unique among the pending requests: the service remembers the ones it applied by it. */
  readonly id: string;
  readonly action: PluginRequestActionName;
  /** The prepared copy to install. Only for an installation. */
  readonly package?: string;
  /** The fingerprint the user confirmed. Only for an installation. */
  readonly sha256?: string;
  /** The cleaned name of the file the user chose. Only for an installation. */
  readonly packageName?: string;
}

/** The requests after a change, and the prepared copies that no request names any more. */
export interface RequestChange {
  readonly requests: readonly PluginRequestEntry[];
  readonly discardedPackages: readonly string[];
}

// The same rules as the service: an entry it would ignore is never written.
const REQUEST_ID_PATTERN = /^[A-Za-z0-9-]{1,64}$/u;
const PLUGIN_ID_PATTERN = /^[a-z][a-z0-9]*(-[a-z0-9]+)*$/u;
const MAXIMUM_PLUGIN_ID_LENGTH = 64;
// The names Windows gives to devices, as PluginManifestReader refuses them: never the folder of a plugin.
const RESERVED_PLUGIN_IDS: ReadonlySet<string> = new Set([
  'con',
  'prn',
  'aux',
  'nul',
  ...Array.from({ length: 10 }, (_unused, digit) => [`com${String(digit)}`, `lpt${String(digit)}`]).flat(),
]);
const SHA256_PATTERN = /^[0-9a-f]{64}$/u;
const MAXIMUM_PACKAGE_NAME_LENGTH = 260;
const ACTIONS: readonly string[] = ['install', 'uninstall', 'enable', 'disable'] satisfies PluginRequestActionName[];
const ORIGINS: readonly string[] = ['shipped', 'installed'] satisfies PluginOriginName[];
// Larger is not a file of requests: the service refuses it too.
const MAXIMUM_FILE_BYTES = 1024 * 1024;
// What a file that is not JSON, or too large to be one of ours, reads as.
const DAMAGED = Symbol('damaged');

/** Whether a value from the page names a plugin: an origin and an identifier, never a path. */
export function isPluginTarget(value: unknown): value is PluginTarget {
  if (typeof value !== 'object' || value === null) {
    return false;
  }
  const { origin, pluginId } = value as Record<string, unknown>;
  return (
    typeof origin === 'string' &&
    ORIGINS.includes(origin) &&
    typeof pluginId === 'string' &&
    pluginId.length <= MAXIMUM_PLUGIN_ID_LENGTH &&
    PLUGIN_ID_PATTERN.test(pluginId) &&
    !RESERVED_PLUGIN_IDS.has(pluginId)
  );
}

function textOf(entry: Record<string, unknown>, name: string): string | undefined {
  const value = entry[name];
  return typeof value === 'string' ? value : undefined;
}

// Read property by property, as the service does: what one entry holds cannot spoil the others.
function toEntry(value: unknown): PluginRequestEntry | undefined {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return undefined;
  }
  const entry = value as Record<string, unknown>;
  const id = textOf(entry, 'id');
  const action = textOf(entry, 'action');
  const target = { origin: textOf(entry, 'origin'), pluginId: textOf(entry, 'pluginId') };
  if (id === undefined || !REQUEST_ID_PATTERN.test(id) || action === undefined || !ACTIONS.includes(action)) {
    return undefined;
  }
  if (!isPluginTarget(target)) {
    return undefined;
  }
  const request = { id, action: action as PluginRequestActionName, ...target };
  if (request.action !== 'install') {
    return request;
  }
  const stagedName = textOf(entry, 'package');
  const sha256 = textOf(entry, 'sha256');
  const packageName = textOf(entry, 'packageName');
  return isStagedName(stagedName) &&
    sha256 !== undefined &&
    SHA256_PATTERN.test(sha256) &&
    packageName !== undefined &&
    packageName.length > 0 &&
    packageName.length <= MAXIMUM_PACKAGE_NAME_LENGTH
    ? { ...request, package: stagedName, sha256, packageName }
    : undefined;
}

async function readJson(file: string): Promise<unknown> {
  let content: Buffer;
  try {
    content = await readFile(file);
  } catch (error) {
    if (isMissing(error)) {
      return undefined;
    }
    throw error;
  }
  // In bytes, as the service counts them.
  if (content.length > MAXIMUM_FILE_BYTES) {
    return DAMAGED;
  }
  try {
    return JSON.parse(content.toString('utf8')) as unknown;
  } catch {
    return DAMAGED;
  }
}

/**
 * Reads the requests the app wrote; none when the file does not exist. A damaged file (not JSON, too large) holds
 * nothing the service would apply: it counts as one entry dropped, and the next write replaces it.
 * @returns The valid requests, in the order of the file, and how many entries were not valid and are dropped.
 * @throws when the file cannot be read.
 */
export async function readRequests(file: string): Promise<{ requests: PluginRequestEntry[]; ignored: number }> {
  const content = await readJson(file);
  if (content === DAMAGED) {
    return { requests: [], ignored: 1 };
  }
  const entries = (content as { requests?: unknown } | undefined)?.requests;
  if (!Array.isArray(entries)) {
    return { requests: [], ignored: content === undefined ? 0 : 1 };
  }
  const requests = entries.map(toEntry).filter((entry) => entry !== undefined);
  return { requests, ignored: entries.length - requests.length };
}

/**
 * The identifiers of the requests the service applied at its last start, from the state it wrote. An unreadable
 * state tells nothing: every request is then still pending.
 */
export async function readAppliedRequestIds(stateFile: string): Promise<ReadonlySet<string>> {
  try {
    const content = await readJson(stateFile);
    const applied =
      content === DAMAGED ? undefined : (content as { appliedRequests?: unknown } | undefined)?.appliedRequests;
    return new Set(Array.isArray(applied) ? applied.filter((id) => typeof id === 'string') : []);
  } catch {
    return new Set();
  }
}

/** Whether a request concerns a plugin: the same origin and the same identifier. */
export function concerns(request: PluginTarget, target: PluginTarget): boolean {
  return request.origin === target.origin && request.pluginId === target.pluginId;
}

/** A new request, with an identifier of its own. */
export function newRequest(action: PluginRequestActionName, target: PluginTarget): PluginRequestEntry {
  return { id: randomUUID(), action, origin: target.origin, pluginId: target.pluginId };
}

/** Removes the requests waiting for a plugin. */
export function cancelRequest(requests: readonly PluginRequestEntry[], target: PluginTarget): RequestChange {
  return {
    requests: requests.filter((pending) => !concerns(pending, target)),
    discardedPackages: requests
      .filter((pending) => concerns(pending, target))
      .map((pending) => pending.package)
      .filter((name) => name !== undefined),
  };
}

/**
 * Puts a request in place of those waiting for the same plugin: the last one replaces the previous one.
 * @param redundant Whether the request asks for what is already so (enable an enabled plugin, uninstall one that is
 * not installed): it then only cancels the one waiting.
 */
export function placeRequest(
  requests: readonly PluginRequestEntry[],
  request: PluginRequestEntry,
  redundant: boolean,
): RequestChange {
  const cancelled = cancelRequest(requests, request);
  return redundant ? cancelled : { ...cancelled, requests: [...cancelled.requests, request] };
}

/** The requests the service has not applied yet. */
export function pendingRequests(
  requests: readonly PluginRequestEntry[],
  applied: ReadonlySet<string>,
): PluginRequestEntry[] {
  return requests.filter((request) => !applied.has(request.id));
}

/** What the page may know of a request: never the name of the prepared copy, nor the fingerprint. */
export function describeRequest({ action, origin, pluginId, packageName }: PluginRequestEntry): PendingPluginRequest {
  // Cleaned when written, and again here: another program may have changed the file since.
  return packageName === undefined
    ? { action, origin, pluginId }
    : { action, origin, pluginId, packageName: cleanDisplayText(packageName) };
}

/**
 * Writes the requests in full or not at all: a temporary file next to it, flushed to the disk, then a replacement.
 * The service may read the file at any time.
 */
export async function writeRequests(file: string, requests: readonly PluginRequestEntry[]): Promise<void> {
  await mkdir(dirname(file), { recursive: true });
  const temporary = `${file}.${randomBytes(8).toString('hex')}.tmp`;
  try {
    const handle = await open(temporary, 'wx');
    try {
      await handle.writeFile(`${JSON.stringify({ requests }, undefined, 2)}\n`, 'utf8');
      await handle.sync();
    } finally {
      await handle.close();
    }
    // An antivirus or the indexer often opens a file just written: the replacement waits for it.
    await renameWithRetries(temporary, file);
  } finally {
    await rm(temporary, { force: true });
  }
}

/**
 * Deletes the temporary files of requests that a write cut short (the app closed during it) left behind. There is only
 * one app, and it writes one request at a time: none of them is on its way.
 */
export async function deleteTemporaryRequestFiles(file: string): Promise<void> {
  let entries: string[];
  try {
    entries = await readdir(dirname(file));
  } catch (error) {
    if (isMissing(error)) {
      return;
    }
    throw error;
  }
  const prefix = `${basename(file)}.`;
  for (const entry of entries) {
    if (entry.startsWith(prefix) && entry.endsWith('.tmp')) {
      await rm(join(dirname(file), entry), { force: true });
    }
  }
}
