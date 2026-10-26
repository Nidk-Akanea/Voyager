/**
 * Replaces the user's home folder by "~" in a log line. Paths in error messages hold the name of the
 * user's account, and a log file may end up attached to a bug report in a public repository.
 */
export function redactHomeFolder(text: string, homeFolder: string): string {
  // "/" and "" would turn every slash of the text into a "~".
  if (homeFolder.length < 2) {
    return text;
  }
  // Windows paths come with backslashes or slashes, in any case. The folder must end there:
  // C:\Users\ann is not the start of C:\Users\anna.
  const variants = new Set([homeFolder, homeFolder.replaceAll('\\', '/'), homeFolder.replaceAll('/', '\\')]);
  const pattern = new RegExp(`(?:${[...variants].map(escapeForPattern).join('|')})(?=[\\\\/]|$|[^\\w.-])`, 'gi');
  return text.replace(pattern, '~');
}

/**
 * What a log call is given, with the home folder hidden: in text, and in the message and stack of an
 * error (the stack of an uncaught error names the folder the app is installed in).
 */
export function redactLogData(data: readonly unknown[], homeFolder: string): unknown[] {
  return data.map((item) => {
    if (typeof item === 'string') {
      return redactHomeFolder(item, homeFolder);
    }
    if (item instanceof Error) {
      return redactHomeFolder(item.stack ?? `${item.name}: ${item.message}`, homeFolder);
    }
    return item;
  });
}

function escapeForPattern(text: string): string {
  return text.replaceAll(/[.*+?^${}()|[\]\\]/g, String.raw`\$&`);
}
