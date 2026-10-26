// Electron fuses of the installed app: switches burnt into the executable that no setting, command
// line or environment variable of the user's machine can turn back on. They close the ways to run
// other code with the app's own executable (docs/conventions/typescript.md).
import { FuseState, FuseV1Options, FuseVersion, type FuseConfig } from '@electron/fuses';

export const APP_FUSES = {
  version: FuseVersion.V1,
  // A fuse added by a later Electron version must be decided here before the app builds.
  strictlyRequireAllFuses: true,
  // Voyager.exe never runs as plain Node.js (ELECTRON_RUN_AS_NODE): no script can borrow it.
  [FuseV1Options.RunAsNode]: false,
  [FuseV1Options.EnableCookieEncryption]: true,
  // NODE_OPTIONS and --inspect would load or expose code of the user's choosing in the main process.
  [FuseV1Options.EnableNodeOptionsEnvironmentVariable]: false,
  [FuseV1Options.EnableNodeCliInspectArguments]: false,
  // The code that runs is the code of app.asar, checked against the hash embedded at build time.
  [FuseV1Options.EnableEmbeddedAsarIntegrityValidation]: true,
  [FuseV1Options.OnlyLoadAppFromAsar]: true,
  [FuseV1Options.LoadBrowserProcessSpecificV8Snapshot]: false,
  // The app only serves its pages through its own voyager:// scheme, never from file://.
  [FuseV1Options.GrantFileProtocolExtraPrivileges]: false,
  [FuseV1Options.WasmTrapHandlers]: true,
} as const satisfies FuseConfig;

/**
 * The fuses of an executable that differ from {@link APP_FUSES}, as their numbers. A fuse the
 * executable does not have (removed, or a wire too short) differs too: nothing is assumed off.
 */
export function findWrongFuses(wire: Readonly<Record<string, unknown>>): string[] {
  const versionDiffers = wire.version === FuseVersion.V1 ? [] : ['version'];
  const fusesDiffer = Object.entries(APP_FUSES)
    .filter(([key]) => /^\d+$/.test(key))
    .filter(([key, expected]) => wire[key] !== (expected ? FuseState.ENABLE : FuseState.DISABLE))
    .map(([key]) => key);
  return [...versionDiffers, ...fusesDiffer];
}
