import { createHash } from 'node:crypto';
import { mkdir, mkdtemp, readdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import {
  ContractRole,
  ContractUseState,
  PackageInstallKind,
  PackageRefusal,
  PluginOrigin,
  PluginStatus,
  PluginVerification,
  type PluginManagementBridge,
  type RestartResult,
} from '@voyager/grpc-client';

import type { ConfirmationBox, DialogAnswer } from './confirmations';
import {
  createPluginManagement,
  describeFailure,
  type ListedPlugin,
  type PackageExamination,
  type PluginManagementFolders,
} from './pluginManagement';
import type { PluginRequestEntry } from './requests';

const PACKAGE_NAME = 'tool.voyagerplugin';
const PACKAGE_BYTES = 'the bytes of the tool package';
const PACKAGE_SHA256 = createHash('sha256').update(PACKAGE_BYTES).digest('hex');
const CLOCK = { origin: 'shipped', pluginId: 'diagnostics-clock' } as const;
const TOOL = { origin: 'installed', pluginId: 'tool' } as const;

function listed(
  id: string,
  origin: PluginOrigin,
  status = PluginStatus.LOADED,
  verification = PluginVerification.GUARANTEED_BY_APPLICATION,
): ListedPlugin {
  return { id, folder: id, version: '1.0.0', origin, status, verification };
}

function examination(overrides: Partial<PackageExamination> = {}): PackageExamination {
  return {
    sha256: PACKAGE_SHA256,
    refusal: PackageRefusal.UNSPECIFIED,
    id: 'tool',
    version: '1.2.0',
    contracts: [{ contract: 'plugin', version: 1, role: ContractRole.REQUIRED, state: ContractUseState.SUPPORTED }],
    hasDotnet: true,
    hasUi: false,
    verification: PluginVerification.UNVERIFIED,
    duplicateOfShipped: false,
    installedVersion: '',
    installKind: PackageInstallKind.NEW,
    ...overrides,
  };
}

let folder: string;
let folders: PluginManagementFolders;
let plugins: ListedPlugin[];
let examine: (stagedName: string) => Promise<PackageExamination>;
let answers: DialogAnswer[];
let shown: ConfirmationBox[];
let chosenFile: string | undefined;
let opened: string[];
let logged: string[];
let serviceReady: boolean;
let restartService: () => Promise<RestartResult>;
let examineThen: (() => Promise<void>) | undefined;
let management: PluginManagementBridge;

const CONFIRMED: DialogAnswer = { response: 1, checkboxChecked: true };
const CANCELLED: DialogAnswer = { response: 0, checkboxChecked: false };

beforeEach(async () => {
  folder = await mkdtemp(join(tmpdir(), 'voyager-plugin-management-'));
  folders = {
    inbox: join(folder, 'inbox'),
    staging: join(folder, 'staging'),
    requestsFile: join(folder, 'requests.json'),
    stateFile: join(folder, 'state.json'),
  };
  plugins = [listed('diagnostics-clock', PluginOrigin.SHIPPED)];
  examine = () => Promise.resolve(examination());
  answers = [];
  shown = [];
  chosenFile = undefined;
  opened = [];
  logged = [];
  serviceReady = true;
  examineThen = undefined;
  restartService = () => Promise.resolve({ outcome: 'restarting' });
  management = createPluginManagement({
    folders,
    listPlugins: () => Promise.resolve(plugins),
    examinePackage: (stagedName) => examine(stagedName),
    showDialog: async (box) => {
      shown.push(box);
      await examineThen?.();
      return answers.shift() ?? CANCELLED;
    },
    chooseFile: () => Promise.resolve(chosenFile),
    openFolder: (path) => {
      opened.push(path);
      return Promise.resolve();
    },
    restartService: () => restartService(),
    isServiceReady: () => serviceReady,
    locale: () => 'fr',
    log: {
      info: (message) => logged.push(message),
      warn: (message) => logged.push(message),
      error: (message) => logged.push(message),
    },
  });
});

afterEach(async () => {
  await rm(folder, { recursive: true, force: true });
});

async function putInInbox(name = PACKAGE_NAME, content = PACKAGE_BYTES): Promise<void> {
  await mkdir(folders.inbox, { recursive: true });
  await writeFile(join(folders.inbox, name), content);
}

/** Confirms the next dialog, after running something while it is open. */
function showDialogThen(whileOpen: () => Promise<void>): void {
  examineThen = whileOpen;
  answers = [CONFIRMED];
}

async function writtenRequests(): Promise<PluginRequestEntry[]> {
  try {
    return (JSON.parse(await readFile(folders.requestsFile, 'utf8')) as { requests: PluginRequestEntry[] }).requests;
  } catch {
    return [];
  }
}

async function stagedCopies(): Promise<string[]> {
  try {
    return await readdir(folders.staging);
  } catch {
    return [];
  }
}

describe('install', () => {
  it('writes the request to install once the user confirms, with the prepared copy and its fingerprint', async () => {
    await putInInbox();
    answers = [CONFIRMED];

    expect(await management.install(PACKAGE_NAME)).toEqual({ outcome: 'requested' });

    const [request] = await writtenRequests();
    expect(request).toMatchObject({
      action: 'install',
      origin: 'installed',
      pluginId: 'tool',
      sha256: PACKAGE_SHA256,
      packageName: PACKAGE_NAME,
    });
    expect(await stagedCopies()).toEqual([request?.package]);
  });

  it('examines the prepared copy, never the file of the inbox', async () => {
    await putInInbox();
    const examined: string[] = [];
    examine = (stagedName) => {
      examined.push(stagedName);
      return Promise.resolve(examination());
    };
    answers = [CONFIRMED];

    await management.install(PACKAGE_NAME);

    expect(examined).toHaveLength(1);
    expect(examined[0]).toMatch(/^[0-9a-f]{32}\.voyagerplugin$/u);
  });

  it('writes nothing and deletes the prepared copy when the user cancels', async () => {
    await putInInbox();
    answers = [CANCELLED];

    expect(await management.install(PACKAGE_NAME)).toEqual({ outcome: 'cancelled' });

    expect(await writtenRequests()).toEqual([]);
    expect(await stagedCopies()).toEqual([]);
  });

  it('shows the warning and waits for "I understand" before writing the request', async () => {
    await putInInbox();
    answers = [{ response: 1, checkboxChecked: false }, CANCELLED];

    expect(await management.install(PACKAGE_NAME)).toEqual({ outcome: 'cancelled' });

    expect(shown).toHaveLength(2);
    expect(shown[0]?.detail).toContain('L’auteur de ce plugin n’est pas vérifié.');
    expect(shown[0]?.requiresCheckbox).toBe(true);
    expect(await writtenRequests()).toEqual([]);
  });

  it('refuses a package the examination refuses, with its reason, and keeps no copy', async () => {
    await putInInbox();
    examine = () => Promise.resolve(examination({ refusal: PackageRefusal.FORBIDDEN_ENTRY, id: '', version: '' }));

    expect(await management.install(PACKAGE_NAME)).toEqual({ outcome: 'refused', reason: 'forbidden-entry' });

    expect(shown).toEqual([]);
    expect(await stagedCopies()).toEqual([]);
  });

  it('refuses a package with a refusal this version does not know', async () => {
    await putInInbox();
    examine = () => Promise.resolve(examination({ refusal: 99 as unknown as PackageRefusal }));

    expect(await management.install(PACKAGE_NAME)).toEqual({ outcome: 'refused', reason: 'unknown' });
    expect(shown).toEqual([]);
  });

  it('shows a replacement for an installation kind this version does not know, when a version is installed', async () => {
    await putInInbox();
    examine = () =>
      Promise.resolve(examination({ installKind: 99 as unknown as PackageInstallKind, installedVersion: '1.0.0' }));
    answers = [CANCELLED];

    await management.install(PACKAGE_NAME);

    expect(shown[0]?.message).toBe('Remplacer la version 1.0.0 du plugin « tool » par la version 1.2.0 ?');
  });

  it('writes nothing when the prepared copy is gone once the user confirms', async () => {
    await putInInbox();
    showDialogThen(async () => {
      await rm(folders.staging, { recursive: true, force: true });
    });

    expect(await management.install(PACKAGE_NAME)).toEqual({ outcome: 'failed' });
    expect(await writtenRequests()).toEqual([]);
  });

  it('refuses a package whose copy is not the one examined', async () => {
    await putInInbox();
    examine = () => Promise.resolve(examination({ sha256: 'c'.repeat(64) }));

    expect(await management.install(PACKAGE_NAME)).toEqual({ outcome: 'refused', reason: 'package-changed' });
    expect(await stagedCopies()).toEqual([]);
  });

  it('says a package gone from the inbox is not found, and installs nothing', async () => {
    expect(await management.install(PACKAGE_NAME)).toEqual({ outcome: 'not-found' });
    expect(await management.install('../outside.voyagerplugin')).toEqual({ outcome: 'not-found' });
    expect(await writtenRequests()).toEqual([]);
  });

  it('answers "failed" and keeps no copy when the service cannot examine the package', async () => {
    await putInInbox();
    examine = () => Promise.reject(Object.assign(new Error('C:\\Users\\Traveler A\\secret'), { code: 14 }));

    expect(await management.install(PACKAGE_NAME)).toEqual({ outcome: 'failed' });

    expect(await stagedCopies()).toEqual([]);
    expect(logged.join('\n')).not.toContain('Traveler A');
  });

  it('keeps the second of two installations, and deletes the copy of the first', async () => {
    await putInInbox();
    answers = [CONFIRMED, CONFIRMED];

    await management.install(PACKAGE_NAME);
    await management.install(PACKAGE_NAME);

    const requests = await writtenRequests();
    expect(requests).toHaveLength(1);
    expect(await stagedCopies()).toEqual([requests[0]?.package]);
  });

  it('logs the identifier, the version and the fingerprint, never the name of the file', async () => {
    await putInInbox('Trip of Traveler A.voyagerplugin');
    answers = [CONFIRMED];

    await management.install('Trip of Traveler A.voyagerplugin');

    expect(logged.join('\n')).toContain(`tool 1.2.0`);
    expect(logged.join('\n')).toContain(PACKAGE_SHA256);
    expect(logged.join('\n')).not.toContain('Traveler A');
  });
});

describe('uninstall', () => {
  it('writes the request once the user confirms', async () => {
    plugins.push(listed('tool', PluginOrigin.INSTALLED, PluginStatus.LOADED, PluginVerification.UNVERIFIED));
    answers = [CONFIRMED];

    expect(await management.uninstall(TOOL)).toEqual({ outcome: 'requested' });

    expect(await writtenRequests()).toMatchObject([{ action: 'uninstall', ...TOOL }]);
    expect(shown[0]?.message).toBe('Désinstaller le plugin « tool » (version 1.0.0) ?');
  });

  it('writes nothing when the user cancels', async () => {
    plugins.push(listed('tool', PluginOrigin.INSTALLED));
    answers = [CANCELLED];

    expect(await management.uninstall(TOOL)).toEqual({ outcome: 'cancelled' });
    expect(await writtenRequests()).toEqual([]);
  });

  it('cancels the installation of a plugin not installed yet, without asking', async () => {
    await putInInbox();
    answers = [CONFIRMED];
    await management.install(PACKAGE_NAME);
    shown.length = 0;

    expect(await management.uninstall(TOOL)).toEqual({ outcome: 'nothing-to-apply' });

    expect(shown).toEqual([]);
    expect(await writtenRequests()).toEqual([]);
    expect(await stagedCopies()).toEqual([]);
  });

  it('never uninstalls a shipped plugin', async () => {
    expect(await management.uninstall(CLOCK)).toEqual({ outcome: 'not-allowed' });
    expect(shown).toEqual([]);
  });

  it('says a plugin the service does not list is not found', async () => {
    expect(await management.uninstall(TOOL)).toEqual({ outcome: 'not-found' });
    expect(await management.uninstall({ origin: 'installed', pluginId: '..\\tool' } as never)).toEqual({
      outcome: 'not-found',
    });
  });
});

describe('disable and enable', () => {
  it('disables without asking', async () => {
    expect(await management.disable(CLOCK)).toEqual({ outcome: 'requested' });

    expect(shown).toEqual([]);
    expect(await writtenRequests()).toMatchObject([{ action: 'disable', ...CLOCK }]);
  });

  it('asks before enabling a disabled plugin, and writes nothing when the user declines', async () => {
    plugins = [listed('diagnostics-clock', PluginOrigin.SHIPPED, PluginStatus.DISABLED)];
    answers = [CANCELLED];

    expect(await management.enable(CLOCK)).toEqual({ outcome: 'cancelled' });

    expect(shown).toHaveLength(1);
    expect(shown[0]?.requiresCheckbox).toBe(false);
    expect(await writtenRequests()).toEqual([]);
  });

  it('enables a disabled plugin once the user confirms', async () => {
    plugins = [listed('diagnostics-clock', PluginOrigin.SHIPPED, PluginStatus.DISABLED)];
    answers = [CONFIRMED];

    expect(await management.enable(CLOCK)).toEqual({ outcome: 'requested' });
    expect(await writtenRequests()).toMatchObject([{ action: 'enable', ...CLOCK }]);
  });

  it('cancels both when enabling a plugin whose disabling waits, without asking', async () => {
    await management.disable(CLOCK);

    expect(await management.enable(CLOCK)).toEqual({ outcome: 'nothing-to-apply' });

    expect(shown).toEqual([]);
    expect(await writtenRequests()).toEqual([]);
  });

  it('writes nothing when disabling a plugin already disabled', async () => {
    plugins = [listed('diagnostics-clock', PluginOrigin.SHIPPED, PluginStatus.DISABLED)];

    expect(await management.disable(CLOCK)).toEqual({ outcome: 'nothing-to-apply' });
    expect(await writtenRequests()).toEqual([]);
  });
});

describe('cancelRequest and listRequests', () => {
  it('cancels the request waiting for a plugin', async () => {
    await management.disable(CLOCK);

    expect(await management.cancelRequest(CLOCK)).toEqual({ outcome: 'nothing-to-apply' });
    expect(await management.listRequests()).toEqual([]);
  });

  it('cancels an installation, and deletes its prepared copy', async () => {
    await putInInbox();
    answers = [CONFIRMED];
    await management.install(PACKAGE_NAME);

    await management.cancelRequest(TOOL);

    expect(await writtenRequests()).toEqual([]);
    expect(await stagedCopies()).toEqual([]);
  });

  it('lists the pending requests, without the prepared copy nor the fingerprint', async () => {
    await putInInbox();
    answers = [CONFIRMED];
    await management.install(PACKAGE_NAME);
    await management.disable(CLOCK);

    expect(await management.listRequests()).toEqual([
      { action: 'install', origin: 'installed', pluginId: 'tool', packageName: PACKAGE_NAME },
      { action: 'disable', origin: 'shipped', pluginId: 'diagnostics-clock' },
    ]);
  });

  it('drops the requests the service applied when it writes the next one', async () => {
    await management.disable(CLOCK);
    const [applied] = await writtenRequests();
    await writeFile(folders.stateFile, JSON.stringify({ plugins: [], appliedRequests: [applied?.id] }));

    expect(await management.listRequests()).toEqual([]);

    plugins = [listed('diagnostics-clock', PluginOrigin.SHIPPED, PluginStatus.DISABLED)];
    answers = [CONFIRMED];
    await management.enable(CLOCK);

    expect(await writtenRequests()).toMatchObject([{ action: 'enable', ...CLOCK }]);
  });
});

describe('the inbox', () => {
  it('lists the packages of the inbox', async () => {
    await putInInbox();

    expect((await management.listInbox()).map((entry) => entry.name)).toEqual([PACKAGE_NAME]);
  });

  it('adds the file the user chose', async () => {
    chosenFile = join(folder, PACKAGE_NAME);
    await writeFile(chosenFile, PACKAGE_BYTES);

    expect(await management.addPackage()).toEqual({ outcome: 'added', name: PACKAGE_NAME, displayName: PACKAGE_NAME });
  });

  it('answers "failed" when the dialog to choose a file fails', async () => {
    const failing = createPluginManagement({
      folders,
      listPlugins: () => Promise.resolve(plugins),
      examinePackage: examine,
      showDialog: () => Promise.resolve(CANCELLED),
      chooseFile: () => Promise.reject(new Error('C:\\Users\\Traveler A')),
      openFolder: () => Promise.resolve(),
      restartService: () => Promise.resolve({ outcome: 'restarting' }),
      isServiceReady: () => true,
      locale: () => 'fr',
      log: { info: () => undefined, warn: () => undefined, error: (message) => logged.push(message) },
    });

    expect(await failing.addPackage()).toEqual({ outcome: 'failed' });
    expect(logged.join('\n')).not.toContain('Traveler A');
  });

  it('adds nothing when the user cancels the choice', async () => {
    expect(await management.addPackage()).toEqual({ outcome: 'cancelled' });
  });

  it('refuses a file that is not a package', async () => {
    chosenFile = join(folder, 'notes.txt');
    await writeFile(chosenFile, 'notes');

    expect(await management.addPackage()).toEqual({ outcome: 'refused', reason: 'not-a-package' });
  });

  it('opens the inbox, creating it first', async () => {
    await management.openInbox();

    expect(opened).toEqual([folders.inbox]);
    expect(await readdir(folders.inbox)).toEqual([]);
  });
});

describe('while the local service starts', () => {
  it('changes no request: the service may be reading them', async () => {
    await management.disable(CLOCK);
    serviceReady = false;

    expect(await management.cancelRequest(CLOCK)).toEqual({ outcome: 'service-not-ready' });
    expect(await management.disable(CLOCK)).toEqual({ outcome: 'service-not-ready' });
    expect(await writtenRequests()).toMatchObject([{ action: 'disable', ...CLOCK }]);
  });

  it('runs the next operation only once the restart is over', async () => {
    await management.disable(CLOCK);
    let finishRestart = (): void => undefined;
    restartService = () =>
      new Promise((resolve) => {
        finishRestart = () => {
          resolve({ outcome: 'restarting' });
        };
      });
    const order: string[] = [];

    const restarting = management.restart().then(() => order.push('restart'));
    const cancelling = management.cancelRequest(CLOCK).then(() => order.push('cancel'));
    await new Promise((resolve) => setTimeout(resolve, 20));
    finishRestart();
    await Promise.all([restarting, cancelling]);

    expect(order).toEqual(['restart', 'cancel']);
  });
});

describe('one operation at a time', () => {
  it('keeps both of two requests made at once', async () => {
    plugins.push(listed('tool', PluginOrigin.INSTALLED));

    await Promise.all([management.disable(CLOCK), management.disable(TOOL)]);

    expect(await writtenRequests()).toHaveLength(2);
  });
});

describe('restart', () => {
  it('answers "failed", without a path, when the requests cannot be read', async () => {
    await mkdir(folders.requestsFile);

    expect(await management.restart()).toEqual({ outcome: 'failed' });
    expect(logged.join('\n')).not.toContain(folder);
  });

  it('answers "failed" when the service cannot be restarted', async () => {
    await management.disable(CLOCK);
    restartService = () => Promise.reject(new Error('C:\\Users\\Traveler A'));

    expect(await management.restart()).toEqual({ outcome: 'failed' });
    expect(logged.join('\n')).not.toContain('Traveler A');
  });

  it('restarts the local service when a request waits', async () => {
    await management.disable(CLOCK);

    expect(await management.restart()).toEqual({ outcome: 'restarting' });
  });

  it('restarts nothing when no request waits', async () => {
    expect(await management.restart()).toEqual({ outcome: 'nothing-to-apply' });
  });
});

describe('describeFailure', () => {
  it('gives the kind and the code of an error, never its message', () => {
    const error = Object.assign(new Error("ENOENT: no such file, open 'C:\\Users\\Traveler A\\x'"), { code: 'ENOENT' });

    expect(describeFailure(error)).toBe('Error (ENOENT)');
  });

  it('gives the kind of an error without a code', () => {
    expect(describeFailure(new TypeError('C:\\Users\\Traveler A'))).toBe('TypeError');
  });
});
