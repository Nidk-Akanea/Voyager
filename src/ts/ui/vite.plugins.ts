// Vite plugins of the user interface that make plugins possible (ADR-0015): the modules the page shares with them, and
// the import map that tells their modules where those are. The files of the plugins come from the app's protocol, in
// development too: the main process serves the version the server loaded (src/ts/desktop/src/main/interfaceModules.ts).
import { resolve } from 'node:path';

import type { Plugin } from 'vite';

/** What a plugin may import, and the module of this package that provides it to the page and to the plugins alike. */
export const SHARED_MODULES: Readonly<Record<string, string>> = {
  react: 'src/sharedModules/react.ts',
  'react/jsx-runtime': 'src/sharedModules/react-jsx-runtime.ts',
  'react-dom': 'src/sharedModules/react-dom.ts',
  'react-dom/client': 'src/sharedModules/react-dom-client.ts',
  '@fluentui/react-components': 'src/sharedModules/fluentui-react-components.ts',
  '@fluent/react': 'src/sharedModules/fluent-react.ts',
  '@voyager/plugin-sdk': 'src/sharedModules/voyager-plugin-sdk.ts',
};

const SHARED_FOLDER = 'shared';

/** Name of the built file of a shared module, under the `shared` folder: `src/sharedModules/react.ts` becomes `shared/react`. */
function sharedEntryName(source: string): string {
  return `${SHARED_FOLDER}/${source.replace(/^src\/sharedModules\//, '').replace(/\.ts$/, '')}`;
}

/**
 * The import map of the page: the specifiers a plugin imports, and where the page serves them from. In development
 * Vite serves the source, which imports the optimized copy the page itself uses; once built, it is a file of the build
 * next to the page. Either way the plugins share the copy of the page.
 */
export function createImportMap(development: boolean): string {
  const imports = Object.fromEntries(
    Object.entries(SHARED_MODULES).map(([specifier, source]) => [
      specifier,
      development ? `/${source}` : `./${sharedEntryName(source)}.js`,
    ]),
  );
  return JSON.stringify({ imports });
}

/**
 * Builds the shared modules as entries next to the page, so that Rollup puts each library in one chunk used by both,
 * and puts their import map in the page.
 */
export function sharedModules(): Plugin {
  return {
    name: 'voyager-shared-modules',
    config: (_config, { command }) => {
      if (command !== 'build') {
        return undefined;
      }
      return {
        build: {
          rollupOptions: {
            input: {
              index: resolve(import.meta.dirname, 'index.html'),
              ...Object.fromEntries(
                Object.values(SHARED_MODULES).map((source) => [
                  sharedEntryName(source),
                  resolve(import.meta.dirname, source),
                ]),
              ),
            },
            // The shared modules are entries of the build: every export must stay, whether or not the page uses it.
            preserveEntrySignatures: 'strict',
            output: {
              entryFileNames: (chunk) =>
                chunk.name.startsWith(`${SHARED_FOLDER}/`) ? '[name].js' : 'assets/[name]-[hash].js',
            },
          },
        },
      };
    },
    transformIndexHtml: {
      order: 'post',
      handler: (_html, context) => [
        {
          tag: 'script',
          attrs: { type: 'importmap' },
          children: createImportMap(context.server !== undefined),
          injectTo: 'head-prepend',
        },
      ],
    },
  };
}
