// Maps an address of the voyager://app protocol to a file of the built user interface, without ever
// leaving its folder.
import { isAbsolute, join, normalize, relative, sep } from 'node:path';

const PARENT_FOLDER = '..';
const INDEX_FILE = 'index.html';

/** The file an address path points to, or undefined when it would leave `directory` or is malformed. */
export function resolveUserInterfaceFile(directory: string, urlPath: string): string | undefined {
  let path: string;
  try {
    path = decodeURIComponent(urlPath);
  } catch {
    return undefined;
  }
  const file = normalize(join(directory, path === '/' || path === '' ? INDEX_FILE : path));
  const fromDirectory = relative(directory, file);
  const leavesDirectory =
    fromDirectory === '' ||
    fromDirectory === PARENT_FOLDER ||
    fromDirectory.startsWith(PARENT_FOLDER + sep) ||
    isAbsolute(fromDirectory);
  return leavesDirectory ? undefined : file;
}
