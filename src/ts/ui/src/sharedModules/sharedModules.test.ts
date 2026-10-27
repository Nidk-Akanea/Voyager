import { createRequire } from 'node:module';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

import { createImportMap, SHARED_MODULES } from '../../vite.plugins';
import * as reactShim from './react';
import * as reactDomShim from './react-dom';
import * as reactDomClientShim from './react-dom-client';
import * as jsxRuntimeShim from './react-jsx-runtime';

const require = createRequire(import.meta.url);

// What a plugin may use of a package: its public exports, not the internals that other React packages share.
function publicExports(specifier: string): string[] {
  return Object.keys(require(specifier) as Record<string, unknown>).filter(
    (name) => !name.startsWith('__') && name !== 'default' && name !== 'unstable_useCacheRefresh' && name !== 'browser',
  );
}

describe('shared modules', () => {
  it.each([
    ['react', reactShim],
    ['react/jsx-runtime', jsxRuntimeShim],
    ['react-dom', reactDomShim],
    ['react-dom/client', reactDomClientShim],
  ])('re-exports every public name of %s (CommonJS packages cannot be re-exported with a star)', (specifier, shim) => {
    const shared = Object.keys(shim).filter((name) => name !== 'default');

    expect(shared.sort()).toEqual(
      expect.arrayContaining(
        publicExports(specifier).filter((name) => name !== 'version' || specifier !== 'react-dom/client'),
      ),
    );
  });

  it('points every specifier a plugin may import to a module of this package', () => {
    expect(Object.keys(SHARED_MODULES).sort()).toEqual(
      [
        '@fluent/react',
        '@fluentui/react-components',
        '@voyager/plugin-sdk',
        'react',
        'react-dom',
        'react-dom/client',
        'react/jsx-runtime',
      ].sort(),
    );
    for (const source of Object.values(SHARED_MODULES)) {
      expect(() => require.resolve(join(import.meta.dirname, '..', '..', source))).not.toThrow();
    }
  });
});

describe('createImportMap', () => {
  it('points to the built files next to the page', () => {
    const map = JSON.parse(createImportMap(false)) as { imports: Record<string, string> };

    expect(map.imports.react).toBe('./shared/react.js');
    expect(map.imports['react/jsx-runtime']).toBe('./shared/react-jsx-runtime.js');
    expect(map.imports['@voyager/plugin-sdk']).toBe('./shared/voyager-plugin-sdk.js');
  });

  it('points to the sources served by Vite in development', () => {
    const map = JSON.parse(createImportMap(true)) as { imports: Record<string, string> };

    expect(map.imports.react).toBe('/src/sharedModules/react.ts');
  });
});
