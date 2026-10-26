import { FuseState, FuseV1Options, FuseVersion } from '@electron/fuses';
import { describe, expect, it } from 'vitest';

import { APP_FUSES, findWrongFuses } from './fuses';

describe('APP_FUSES', () => {
  it.each([
    FuseV1Options.RunAsNode,
    FuseV1Options.EnableNodeOptionsEnvironmentVariable,
    FuseV1Options.EnableNodeCliInspectArguments,
    FuseV1Options.GrantFileProtocolExtraPrivileges,
  ])('keeps fuse %s off', (fuse) => {
    expect(APP_FUSES[fuse]).toBe(false);
  });

  it.each([FuseV1Options.OnlyLoadAppFromAsar, FuseV1Options.EnableEmbeddedAsarIntegrityValidation])(
    'keeps fuse %s on',
    (fuse) => {
      expect(APP_FUSES[fuse]).toBe(true);
    },
  );

  it('decides every fuse of the installed Electron version', () => {
    const decided = Object.keys(APP_FUSES).filter((key) => /^\d+$/.test(key));

    // The numeric keys are the fuses; strictlyRequireAllFuses makes the build fail on a missing one.
    expect(decided.length).toBe(Object.values(FuseV1Options).filter((value) => typeof value === 'number').length);
  });
});

describe('findWrongFuses', () => {
  /** The wire of an executable built with exactly the fuses wanted. */
  function expectedWire(): Record<string, unknown> {
    const wire: Record<string, unknown> = { version: FuseVersion.V1 };
    for (const [key, expected] of Object.entries(APP_FUSES)) {
      if (/^\d+$/.test(key)) {
        wire[key] = expected ? FuseState.ENABLE : FuseState.DISABLE;
      }
    }
    return wire;
  }

  it('finds nothing wrong in an executable built as wanted', () => {
    expect(findWrongFuses(expectedWire())).toEqual([]);
  });

  it('finds a fuse switched the wrong way', () => {
    const wire = { ...expectedWire(), [FuseV1Options.RunAsNode]: FuseState.ENABLE };

    expect(findWrongFuses(wire)).toEqual([String(FuseV1Options.RunAsNode)]);
  });

  it.each([undefined, FuseState.REMOVED, FuseState.INHERIT])(
    'does not take a fuse that is %s for one that is off',
    (state) => {
      const wire = { ...expectedWire(), [FuseV1Options.RunAsNode]: state };

      expect(findWrongFuses(wire)).toEqual([String(FuseV1Options.RunAsNode)]);
    },
  );

  it('finds an unknown version of the fuse wire', () => {
    expect(findWrongFuses({ ...expectedWire(), version: '2' })).toEqual(['version']);
  });
});
