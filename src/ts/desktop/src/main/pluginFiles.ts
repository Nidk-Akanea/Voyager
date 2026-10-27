// Maps an address of the voyager://app protocol to a file of a plugin, without ever leaving the folder of that plugin
// and without ever reaching what the interface has no business reading: its manifest, its assemblies (ADR-0015).
import { join } from 'node:path';

import { resolveUserInterfaceFile } from './userInterfaceFiles';

/** A file of a plugin that the interface may read, with the type to answer it with. */
export interface PluginFile {
  readonly file: string;
  readonly contentType: string;
}

const PLUGIN_ID = '[a-z][a-z0-9]*(?:-[a-z0-9]+)*';
// plugins/<id>/ui/...: the interface module of the plugin and what it imports.
const INTERFACE_FILE = new RegExp(`^/plugins/(${PLUGIN_ID})/ui/(.+)$`);
// plugins/<id>/i18n/<language>/messages.ftl: the translations of the plugin, one file per language.
const MESSAGES_FILE = new RegExp(`^/plugins/(${PLUGIN_ID})/i18n/(fr|en)/messages\\.ftl$`);
const SCRIPT_EXTENSIONS = ['.js', '.mjs'];
// A module script is refused by the browser unless it is served as JavaScript, whatever the system thinks of the extension.
const SCRIPT_TYPE = 'text/javascript';
const MESSAGES_TYPE = 'text/plain; charset=utf-8';

/**
 * The file an address path points to, or undefined when the interface may not read it.
 * @param pluginsDirectory Folder that holds one folder per plugin.
 * @param urlPath Path of the address, such as `/plugins/diagnostics-clock/ui/index.js`.
 */
export function resolvePluginFile(pluginsDirectory: string, urlPath: string): PluginFile | undefined {
  const messages = MESSAGES_FILE.exec(urlPath);
  if (messages?.[1] !== undefined && messages[2] !== undefined) {
    return {
      file: join(pluginsDirectory, messages[1], 'i18n', messages[2], 'messages.ftl'),
      contentType: MESSAGES_TYPE,
    };
  }

  const script = INTERFACE_FILE.exec(urlPath);
  if (script?.[1] === undefined || script[2] === undefined) {
    return undefined;
  }
  const file = resolveUserInterfaceFile(join(pluginsDirectory, script[1], 'ui'), `/${script[2]}`);
  if (file === undefined || !SCRIPT_EXTENSIONS.some((extension) => file.endsWith(extension))) {
    return undefined;
  }
  return { file, contentType: SCRIPT_TYPE };
}
