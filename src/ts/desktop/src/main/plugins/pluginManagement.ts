// What the main process does when the user interface asks to manage plugins (ADR-0016): it checks what the page
// sends, asks the user in a native dialog, writes the requests and restarts the local service. The service itself
// only reads (it lists the plugins and examines a prepared package); no change goes through a gRPC call.
import { lstat, mkdir } from 'node:fs/promises';
import { join } from 'node:path';

import {
  ContractRole,
  ContractUseState,
  PackageInstallKind,
  PackageRefusal,
  PluginOrigin,
  PluginStatus,
  PluginVerification,
  type AddPackageResult,
  type ContractUse,
  type ExaminePackageResponse,
  type InboxPackage,
  type InstallRefusal,
  type PendingPluginRequest,
  type Plugin,
  type PluginActionResult,
  type PluginManagementBridge,
  type PluginTarget,
  type RecoveryResult,
  type RestartResult,
} from '@voyager/grpc-client';

import {
  askConfirmation,
  enableConfirmation,
  installConfirmation,
  uninstallConfirmation,
  type ConfirmationBox,
  type DialogAnswer,
  type DialogLocale,
  type InstallFacts,
  type InstallKind,
  type PluginFacts,
  type Verification,
} from './confirmations';
import { cleanDisplayText } from './displayText';
import { isMissing } from './files';
import { addToInbox, findInboxPackage, InboxRefusalError, listInbox } from './inbox';
import {
  addRecoveryRequest,
  cancelRequest,
  concerns,
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
  type RequestChange,
} from './requests';
import { discardStagedPackage, NotEnoughSpaceError, stagePackage } from './staging';

async function isPresent(file: string): Promise<boolean> {
  try {
    await lstat(file);
    return true;
  } catch (error) {
    if (isMissing(error)) {
      return false;
    }
    throw error;
  }
}

/** What the plugin management reads of a plugin the local service lists. */
export type ListedPlugin = Pick<Plugin, 'id' | 'folder' | 'version' | 'origin' | 'status' | 'verification'>;

/** What the plugin management reads of the examination of a package. */
export type PackageExamination = Pick<
  ExaminePackageResponse,
  | 'sha256'
  | 'refusal'
  | 'id'
  | 'version'
  | 'hasDotnet'
  | 'hasUi'
  | 'verification'
  | 'duplicateOfShipped'
  | 'installedVersion'
  | 'installKind'
> & { readonly contracts: readonly Pick<ContractUse, 'contract' | 'version' | 'role' | 'state'>[] };

/** The files and folders of the plugins folder of the user's data that the main process uses. */
export interface PluginManagementFolders {
  /** Where packages wait to be installed. */
  readonly inbox: string;
  /** The prepared copies of the packages being installed. */
  readonly staging: string;
  /** The requests, written by the main process alone. */
  readonly requestsFile: string;
  /** The state, written by the local service alone and only read here. */
  readonly stateFile: string;
}

/** Where the plugin management writes what it does: never a path nor the name of a file the user chose. */
export interface PluginManagementLog {
  readonly info: (message: string) => void;
  readonly warn: (message: string) => void;
  readonly error: (message: string) => void;
}

export interface PluginManagementOptions {
  readonly folders: PluginManagementFolders;
  /** The plugins as the local service lists them; throws when it cannot be asked. */
  readonly listPlugins: () => Promise<readonly ListedPlugin[]>;
  /** Asks the local service to examine a prepared copy; throws when it cannot be asked. */
  readonly examinePackage: (stagedName: string) => Promise<PackageExamination>;
  /** Shows a native dialog. */
  readonly showDialog: (box: ConfirmationBox) => Promise<DialogAnswer>;
  /** Lets the user choose a package file in a native dialog; undefined when they cancel. */
  readonly chooseFile: () => Promise<string | undefined>;
  /** Opens a folder in the file explorer. */
  readonly openFolder: (folder: string) => Promise<void>;
  /**
   * Restarts the local service, and resolves once it is ready or has failed: until then, it may still be reading the
   * requests, and no request is changed.
   */
  readonly restartService: () => Promise<RestartResult>;
  /** Whether the local service is ready: while it starts, it may be reading the requests, and none is changed. */
  readonly isServiceReady: () => boolean;
  /** Whether the local service failed: it runs no more, and so reads no request until it is started again. */
  readonly isServiceFailed: () => boolean;
  /** Starts the local service again after it failed. */
  readonly retryService: () => void;
  /** The language of the dialogs. */
  readonly locale: () => DialogLocale;
  readonly log: PluginManagementLog;
}

const REFUSALS: Readonly<Partial<Record<PackageRefusal, InstallRefusal>>> = {
  [PackageRefusal.UNREADABLE]: 'unreadable',
  [PackageRefusal.MANIFEST_INVALID]: 'manifest-invalid',
  [PackageRefusal.DECLARED_FILE_MISSING]: 'declared-file-missing',
  [PackageRefusal.CONTRACT_NOT_SUPPORTED]: 'contract-not-supported',
  [PackageRefusal.FORBIDDEN_ENTRY]: 'forbidden-entry',
  [PackageRefusal.NOT_ENOUGH_SPACE]: 'not-enough-space',
};

const INSTALL_KINDS: Readonly<Partial<Record<PackageInstallKind, InstallKind>>> = {
  [PackageInstallKind.UPGRADE]: 'upgrade',
  [PackageInstallKind.DOWNGRADE]: 'downgrade',
  [PackageInstallKind.REINSTALL]: 'reinstall',
};

const ORIGINS: Readonly<Record<PluginTarget['origin'], PluginOrigin>> = {
  shipped: PluginOrigin.SHIPPED,
  installed: PluginOrigin.INSTALLED,
};

/**
 * What to write in the log about something that failed: its code (ENOENT, a gRPC code) or its kind. Never its
 * message, which holds paths, and so the name of the user's account.
 */
export function describeFailure(error: unknown): string {
  if (typeof error === 'object' && error !== null) {
    const { code, name } = error as { code?: unknown; name?: unknown };
    if (typeof code === 'string' || typeof code === 'number') {
      return `${typeof name === 'string' ? name : 'Error'} (${String(code)})`;
    }
    if (typeof name === 'string') {
      return name;
    }
  }
  return typeof error;
}

// Only what the application vouches for counts: anything else, including a value this version does not know, is
// "not verified".
function verificationOf(verification: PluginVerification): Verification {
  return verification === PluginVerification.GUARANTEED_BY_APPLICATION ? 'guaranteed' : 'unverified';
}

function installFacts(fileName: string, examination: PackageExamination): InstallFacts {
  return {
    fileName,
    sha256: examination.sha256,
    id: examination.id,
    version: examination.version,
    contracts: examination.contracts.map((use) => ({
      contract: use.contract,
      version: use.version,
      role: use.role === ContractRole.PROVIDED ? 'provides' : 'requires',
      deprecated: use.state === ContractUseState.DEPRECATED,
    })),
    hasDotnet: examination.hasDotnet,
    hasUi: examination.hasUi,
    verification: verificationOf(examination.verification),
    duplicateOfShipped: examination.duplicateOfShipped,
    ...(examination.installedVersion === '' ? {} : { installedVersion: examination.installedVersion }),
    // A kind this version does not know is shown as a replacement when a version is installed: never hidden as new.
    kind: INSTALL_KINDS[examination.installKind] ?? (examination.installedVersion === '' ? 'new' : 'upgrade'),
  };
}

// Closed by default: a refusal this version does not know is a refusal all the same.
function refusalOf(examination: PackageExamination): InstallRefusal | undefined {
  return examination.refusal === PackageRefusal.UNSPECIFIED ? undefined : (REFUSALS[examination.refusal] ?? 'unknown');
}

/** The management of plugins, and the start without the installed plugins of the screen of a service that failed. */
export interface PluginManagement extends PluginManagementBridge {
  /**
   * Writes the request to start without the installed plugins, then starts the local service again. Only once the
   * service failed: it reads no request then.
   */
  readonly startWithoutInstalledPlugins: () => Promise<RecoveryResult>;
}

/** The management of plugins the preload script exposes to the user interface; one operation at a time. */
export function createPluginManagement({
  folders,
  listPlugins,
  examinePackage,
  showDialog,
  chooseFile,
  openFolder,
  restartService,
  isServiceReady,
  isServiceFailed,
  retryService,
  locale,
  log,
}: PluginManagementOptions): PluginManagement {
  // One operation at a time: two would read and write the requests over each other.
  let queue: Promise<unknown> = Promise.resolve();
  const oneAtATime = <T>(operation: () => Promise<T>): Promise<T> => {
    const result = queue.then(operation);
    queue = result.catch(() => undefined);
    return result;
  };

  /**
   * A change of the requests: refused while the service starts (it may be reading them), and whose failure is logged
   * and answered "failed", never thrown at the page with a path in it.
   */
  const guarded = (what: string, operation: () => Promise<PluginActionResult>): Promise<PluginActionResult> =>
    oneAtATime(async () => {
      if (!isServiceReady()) {
        return { outcome: 'service-not-ready' };
      }
      try {
        return await operation();
      } catch (error) {
        log.error(`${what} failed: ${describeFailure(error)}.`);
        return { outcome: 'failed' };
      }
    });

  /**
   * The valid requests the service has not applied yet, the request to start without the installed plugins if it has
   * not applied it either, and how many entries of the file are not valid.
   */
  const readPendingRequests = async (): Promise<{
    pending: PluginRequestEntry[];
    recovery?: string;
    ignored: number;
  }> => {
    const { requests, ignored, recovery } = await readRequests(folders.requestsFile);
    const applied = await readAppliedRequestIds(folders.stateFile);
    return {
      pending: pendingRequests(requests, applied),
      ignored,
      ...(recovery === undefined || applied.has(recovery) ? {} : { recovery }),
    };
  };

  /** Deletes a prepared copy; a failure is only logged, as the service deletes the copies no request names. */
  const discardQuietly = async (stagedName: string): Promise<void> => {
    try {
      await discardStagedPackage(folders.staging, stagedName);
    } catch (error) {
      log.warn(`A prepared copy of a package could not be deleted: ${describeFailure(error)}.`);
    }
  };

  /** Reads the requests still pending, changes them, writes them, and deletes the prepared copies left unnamed. */
  const changeRequests = async (change: (pending: readonly PluginRequestEntry[]) => RequestChange): Promise<void> => {
    // The service remembers the requests it applied only while they are in the file: they go now.
    const { pending, ignored, recovery } = await readPendingRequests();
    if (ignored > 0) {
      log.warn(`${String(ignored)} entries of the requests file are not valid: they are dropped.`);
    }
    const { requests: next, discardedPackages } = change(pending);
    await deleteTemporaryRequestFiles(folders.requestsFile);
    await writeRequests(folders.requestsFile, next, recovery);
    for (const stagedName of discardedPackages) {
      await discardQuietly(stagedName);
    }
  };

  const findPlugin = async (target: PluginTarget): Promise<ListedPlugin | undefined> =>
    (await listPlugins()).find(
      (plugin) => plugin.origin === ORIGINS[target.origin] && plugin.folder === target.pluginId,
    );

  const pluginFacts = (plugin: ListedPlugin): PluginFacts => ({
    id: plugin.id,
    version: plugin.version,
    verification: verificationOf(plugin.verification),
  });

  const pendingFor = async (target: PluginTarget): Promise<PluginRequestEntry | undefined> =>
    (await readPendingRequests()).pending.find((request) => concerns(request, target));

  const install = async (name: unknown): Promise<PluginActionResult> => {
    const source = await findInboxPackage(folders.inbox, name);
    // findInboxPackage checked the name already: the test of its type only tells TypeScript.
    if (source === undefined || typeof name !== 'string') {
      return { outcome: 'not-found' };
    }
    let staged;
    try {
      staged = await stagePackage(source, folders.staging);
    } catch (error) {
      if (error instanceof NotEnoughSpaceError) {
        return { outcome: 'refused', reason: 'not-enough-space' };
      }
      throw error;
    }
    let requested = false;
    try {
      const examination = await examinePackage(staged.stagedName);
      if (examination.sha256 !== staged.sha256) {
        log.warn('A package changed while it was examined: it is refused.');
        return { outcome: 'refused', reason: 'package-changed' };
      }
      const refusal = refusalOf(examination);
      if (refusal !== undefined) {
        log.info(`A package was refused at its examination (${refusal}); fingerprint ${examination.sha256}.`);
        return { outcome: 'refused', reason: refusal };
      }
      const box = installConfirmation(installFacts(name, examination), locale());
      if (!(await askConfirmation(box, showDialog))) {
        log.info(`The user declined to install the plugin ${examination.id} ${examination.version}.`);
        return { outcome: 'cancelled' };
      }
      // The dialog may have stayed open long enough for the service to clean the staging folder.
      if (!(await isPresent(join(folders.staging, staged.stagedName)))) {
        log.warn('The prepared copy of a package was gone once the user confirmed: nothing is requested.');
        return { outcome: 'failed' };
      }
      const request: PluginRequestEntry = {
        ...newRequest('install', { origin: 'installed', pluginId: examination.id }),
        package: staged.stagedName,
        sha256: staged.sha256,
        packageName: cleanDisplayText(name),
      };
      await changeRequests((pending) => placeRequest(pending, request, false));
      requested = true;
      log.info(
        `Installing the plugin ${examination.id} ${examination.version} is requested; fingerprint ${staged.sha256}.`,
      );
      return { outcome: 'requested' };
    } finally {
      // Cancelled, refused or failed: the copy has no use any more.
      if (!requested) {
        await discardQuietly(staged.stagedName);
      }
    }
  };

  const uninstall = async (target: unknown): Promise<PluginActionResult> => {
    if (!isPluginTarget(target)) {
      return { outcome: 'not-found' };
    }
    if (target.origin !== 'installed') {
      return { outcome: 'not-allowed' };
    }
    const plugin = await findPlugin(target);
    if (plugin === undefined) {
      // Not installed yet: uninstalling it cancels its installation, which needs no confirmation.
      if ((await pendingFor(target))?.action === 'install') {
        await changeRequests((pending) => cancelRequest(pending, target));
        log.info(`The installation waiting for the plugin ${target.pluginId} is cancelled.`);
        return { outcome: 'nothing-to-apply' };
      }
      return { outcome: 'not-found' };
    }
    if (!(await askConfirmation(uninstallConfirmation(pluginFacts(plugin), locale()), showDialog))) {
      log.info(`The user declined to uninstall the plugin ${target.pluginId}.`);
      return { outcome: 'cancelled' };
    }
    await changeRequests((pending) => placeRequest(pending, newRequest('uninstall', target), false));
    log.info(`Uninstalling the plugin ${target.pluginId} is requested.`);
    return { outcome: 'requested' };
  };

  const enable = async (target: unknown): Promise<PluginActionResult> => {
    if (!isPluginTarget(target)) {
      return { outcome: 'not-found' };
    }
    const plugin = await findPlugin(target);
    if (plugin === undefined) {
      return { outcome: 'not-found' };
    }
    // Enabling a plugin that is neither disabled nor suspended only cancels a request to disable it: nothing more runs.
    // A suspected plugin asks for more: the dialog recalls the suspicion, and waits for "I understand".
    const suspended = plugin.status === PluginStatus.SUSPENDED;
    const redundant = plugin.status !== PluginStatus.DISABLED && !suspended;
    const box = enableConfirmation(pluginFacts(plugin), locale(), suspended);
    if (!redundant && !(await askConfirmation(box, showDialog))) {
      log.info(`The user declined to enable the plugin ${target.pluginId}.`);
      return { outcome: 'cancelled' };
    }
    await changeRequests((pending) => placeRequest(pending, newRequest('enable', target), redundant));
    log.info(
      redundant
        ? `The plugin ${target.pluginId} is not disabled: only the request waiting for it is cancelled.`
        : `Enabling the plugin ${target.pluginId} is requested.`,
    );
    return { outcome: redundant ? 'nothing-to-apply' : 'requested' };
  };

  const disable = async (target: unknown): Promise<PluginActionResult> => {
    if (!isPluginTarget(target)) {
      return { outcome: 'not-found' };
    }
    const plugin = await findPlugin(target);
    if (plugin === undefined) {
      return { outcome: 'not-found' };
    }
    // Less runs: no confirmation (spec, "Désinstaller, activer, désactiver").
    const redundant = plugin.status === PluginStatus.DISABLED;
    await changeRequests((pending) => placeRequest(pending, newRequest('disable', target), redundant));
    log.info(
      redundant
        ? `The plugin ${target.pluginId} is disabled already: only the request waiting for it is cancelled.`
        : `Disabling the plugin ${target.pluginId} is requested.`,
    );
    return { outcome: redundant ? 'nothing-to-apply' : 'requested' };
  };

  const cancel = async (target: unknown): Promise<PluginActionResult> => {
    if (!isPluginTarget(target)) {
      return { outcome: 'not-found' };
    }
    await changeRequests((pending) => cancelRequest(pending, target));
    log.info(`The request waiting for the plugin ${target.pluginId} is cancelled.`);
    return { outcome: 'nothing-to-apply' };
  };

  return {
    listInbox: () =>
      oneAtATime(async (): Promise<readonly InboxPackage[]> => {
        try {
          return await listInbox(folders.inbox);
        } catch (error) {
          log.error(`The inbox could not be read: ${describeFailure(error)}.`);
          return [];
        }
      }),
    addPackage: () =>
      oneAtATime(async (): Promise<AddPackageResult> => {
        try {
          const source = await chooseFile();
          if (source === undefined) {
            return { outcome: 'cancelled' };
          }
          const name = await addToInbox(source, folders.inbox);
          return { outcome: 'added', name, displayName: cleanDisplayText(name) };
        } catch (error) {
          if (error instanceof InboxRefusalError) {
            return { outcome: 'refused', reason: error.reason };
          }
          log.error(`A package could not be added to the inbox: ${describeFailure(error)}.`);
          return { outcome: 'failed' };
        }
      }),
    openInbox: () =>
      oneAtATime(async () => {
        try {
          await mkdir(folders.inbox, { recursive: true });
          await openFolder(folders.inbox);
        } catch (error) {
          log.error(`The inbox could not be opened: ${describeFailure(error)}.`);
          // Never the error itself, nor as a cause: its message holds the path of the inbox.
          // eslint-disable-next-line preserve-caught-error -- the cause would carry the path to the page
          throw new Error('The inbox could not be opened.');
        }
      }),
    install: (name) => guarded('The installation of a package', () => install(name)),
    uninstall: (target) => guarded('The uninstallation of a plugin', () => uninstall(target)),
    enable: (target) => guarded('Enabling a plugin', () => enable(target)),
    disable: (target) => guarded('Disabling a plugin', () => disable(target)),
    cancelRequest: (target) => guarded('Cancelling a request', () => cancel(target)),
    listRequests: () =>
      oneAtATime(async (): Promise<readonly PendingPluginRequest[]> => {
        try {
          return (await readPendingRequests()).pending.map(describeRequest);
        } catch (error) {
          log.error(`The requests could not be read: ${describeFailure(error)}.`);
          return [];
        }
      }),
    startWithoutInstalledPlugins: () =>
      oneAtATime(async (): Promise<RecoveryResult> => {
        if (!isServiceFailed()) {
          return { outcome: 'not-failed' };
        }
        try {
          for (const stagedName of await addRecoveryRequest(folders.requestsFile, folders.stateFile)) {
            await discardQuietly(stagedName);
          }
        } catch (error) {
          log.error(
            `The request to start without the installed plugins could not be written: ${describeFailure(error)}.`,
          );
          return { outcome: 'failed' };
        }
        log.info('Starting the local service again without the installed plugins.');
        retryService();
        return { outcome: 'restarting' };
      }),
    // Only when a request waits: a module of a plugin could otherwise restart Voyager over and over. The next operation
    // waits until the service is ready again.
    restart: () =>
      oneAtATime(async (): Promise<RestartResult> => {
        try {
          if ((await readPendingRequests()).pending.length === 0) {
            return { outcome: 'nothing-to-apply' };
          }
          const result = await restartService();
          if (result.outcome === 'restart-elsewhere') {
            log.warn('The local service is not run by the app: the requests apply once it is restarted where it runs.');
          }
          return result;
        } catch (error) {
          log.error(`The restart to apply the requests failed: ${describeFailure(error)}.`);
          return { outcome: 'failed' };
        }
      }),
  };
}
