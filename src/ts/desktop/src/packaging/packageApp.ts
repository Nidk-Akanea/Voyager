// Builds the app folder that Velopack packs into an installer (ADR-0009, tools/release.ps1):
// the compiled main process and preload script in an asar archive, the user interface and the
// published local service next to it, and the Electron fuses burnt into the executable.
//
// Compiled by `pnpm build` (vite.packaging.config.ts) and run by tools/release.ps1.
import { cp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { dirname, join, resolve } from 'node:path';
import { parseArgs } from 'node:util';

import { flipFuses, getCurrentFuseWire } from '@electron/fuses';
import { packager } from '@electron/packager';

import { isAllowedUpdateUrl, UPDATE_SOURCE_REQUIREMENT, UPDATE_SOURCE_SETTING } from '../main/updateSource';
import { APP_FUSES, findWrongFuses } from './fuses';

const APP_NAME = 'Voyager';
const PLATFORM = 'win32';
const ARCH = 'x64';
// Not bundled by vite (vite.main.config.ts): velopack holds native code, electron-log reads the file system.
const UNBUNDLED_DEPENDENCIES = ['velopack', 'electron-log'];

interface PackageManifest {
  readonly dependencies?: Readonly<Record<string, string>>;
}

/** Copies a package that is not bundled (native code), and what it needs at run time, into node_modules. */
async function copyRuntimeDependency(
  name: string,
  requireFrom: NodeJS.Require,
  nodeModules: string,
  copied = new Set<string>(),
): Promise<void> {
  if (copied.has(name)) {
    return;
  }
  copied.add(name);
  const manifestPath = requireFrom.resolve(`${name}/package.json`);
  const folder = dirname(manifestPath);
  const target = join(nodeModules, name);
  await mkdir(dirname(target), { recursive: true });
  // pnpm links packages into a store: the files themselves are what the app needs.
  await cp(folder, target, { recursive: true, dereference: true });

  const manifest = JSON.parse(await readFile(manifestPath, 'utf8')) as PackageManifest;
  const dependencyRequire = createRequire(manifestPath);
  for (const dependency of Object.keys(manifest.dependencies ?? {})) {
    await copyRuntimeDependency(dependency, dependencyRequire, nodeModules, copied);
  }
}

/** The files the packaged app is made of: what `vite build` produced, and the code it cannot bundle. */
async function stageApp(
  version: string,
  updateSource: string | undefined,
  packageRoot: string,
  stagingFolder: string,
): Promise<void> {
  await rm(stagingFolder, { recursive: true, force: true });
  await mkdir(stagingFolder, { recursive: true });

  const own = createRequire(join(packageRoot, 'package.json'));
  const manifest = JSON.parse(await readFile(own.resolve('./package.json'), 'utf8')) as PackageManifest;
  const dependencies = Object.fromEntries(
    UNBUNDLED_DEPENDENCIES.map((name) => {
      const wanted = manifest.dependencies?.[name];
      if (wanted === undefined) {
        throw new Error(`${name} is not a dependency of the desktop package.`);
      }
      return [name, wanted];
    }),
  );

  await writeFile(
    join(stagingFolder, 'package.json'),
    JSON.stringify(
      {
        name: 'voyager',
        productName: APP_NAME,
        description: 'Voyager: prepare a group trip with a fair budget.',
        version,
        main: 'dist/main.js',
        type: 'module',
        // The packager looks for who made the app, and which Electron runs it, in this file.
        author: APP_NAME,
        // Where the app looks for updates, if not in the releases of the repository (ADR-0014).
        ...(updateSource === undefined ? {} : { [UPDATE_SOURCE_SETTING]: updateSource }),
        dependencies,
      },
      undefined,
      2,
    ),
  );
  // No source maps: they would only carry the paths of the machine that built the app.
  await cp(join(packageRoot, 'dist'), join(stagingFolder, 'dist'), {
    recursive: true,
    filter: (source) => !source.endsWith('.map'),
  });
  for (const name of UNBUNDLED_DEPENDENCIES) {
    await copyRuntimeDependency(name, own, join(stagingFolder, 'node_modules'));
  }
}

/** The version of Electron installed in the workspace: the one the app is packed with. */
async function installedElectronVersion(packageRoot: string): Promise<string> {
  const manifestPath = createRequire(join(packageRoot, 'package.json')).resolve('electron/package.json');
  return (JSON.parse(await readFile(manifestPath, 'utf8')) as { version: string }).version;
}

/** Fails unless the executable carries exactly the fuses the app was built to have. */
async function verifyFuses(executable: string): Promise<void> {
  const wire = (await getCurrentFuseWire(executable)) as unknown as Record<string, unknown>;
  const wrong = findWrongFuses(wire);
  if (wrong.length > 0) {
    throw new Error(`The executable has fuses that differ from the expected ones: ${wrong.join(', ')}.`);
  }
}

/** Reads the arguments given by tools/release.ps1, then stages, packs and checks the app folder. */
async function main(): Promise<void> {
  const { values } = parseArgs({
    options: {
      version: { type: 'string' },
      service: { type: 'string' },
      ui: { type: 'string' },
      out: { type: 'string' },
      'update-source': { type: 'string' },
    },
  });
  const { version, service, ui, out } = values;
  if (version === undefined || service === undefined || ui === undefined || out === undefined) {
    throw new Error(
      'Usage: packageApp --version <x.y.z> --service <folder> --ui <folder> --out <folder> [--update-source <https address>]',
    );
  }

  const packageRoot = process.cwd();
  const outFolder = resolve(out);
  const stagingFolder = join(outFolder, 'app-source');
  const extraFolder = join(outFolder, 'extra');

  const updateSource = values['update-source'];
  if (updateSource !== undefined && !isAllowedUpdateUrl(updateSource)) {
    throw new Error(`--update-source must be ${UPDATE_SOURCE_REQUIREMENT}.`);
  }

  await stageApp(version, updateSource, packageRoot, stagingFolder);

  // The app finds them in its resources folder: local-service/ and ui/ (src/main/main.ts).
  await rm(extraFolder, { recursive: true, force: true });
  await cp(resolve(service), join(extraFolder, 'local-service'), { recursive: true });
  await cp(resolve(ui), join(extraFolder, 'ui'), { recursive: true });

  const [appFolder] = await packager({
    dir: stagingFolder,
    out: join(outFolder, 'app'),
    name: APP_NAME,
    executableName: APP_NAME,
    platform: PLATFORM,
    arch: ARCH,
    appVersion: version,
    electronVersion: await installedElectronVersion(packageRoot),
    asar: { unpack: '*.node' },
    extraResource: [join(extraFolder, 'local-service'), join(extraFolder, 'ui')],
    overwrite: true,
    quiet: true,
  });
  if (appFolder === undefined) {
    throw new Error('The packager did not produce an app folder.');
  }

  const executable = join(appFolder, `${APP_NAME}.exe`);
  await flipFuses(executable, APP_FUSES);
  await verifyFuses(executable);

  console.log(`App folder: ${appFolder}`);
}

main().catch((error: unknown) => {
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 1;
});
