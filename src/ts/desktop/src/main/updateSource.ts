// Where the installed app looks for updates (ADR-0014): the releases of the repository, unless the
// package was built for another server (tools/release.ps1 -UpdateSource), which is how an update is
// tried without publishing anything. The choice is written in the app's package.json, inside app.asar,
// whose integrity is checked (ADR-0013): nothing on the user's machine can change it.

const LOOPBACK_HOSTS = new Set(['localhost', '127.0.0.1', '[::1]']);

/** The setting of package.json that names the update server. */
export const UPDATE_SOURCE_SETTING = 'voyagerUpdateSource';

/** What an update address must be (completes "must be ..."); the build and the app give the same reason. */
export const UPDATE_SOURCE_REQUIREMENT = 'an https address, or http to this machine, without credentials or parameters';

/**
 * Where to look for updates: a server of the package's own (`http`), the releases of the repository
 * (`repository`), or the repository again because the setting is unusable (`invalid`, with the reason to log).
 */
export type UpdateSourceChoice =
  | { readonly kind: 'http'; readonly url: string }
  | { readonly kind: 'repository' }
  | { readonly kind: 'invalid'; readonly reason: string };

/**
 * Whether an address may serve updates: https, or http to this machine. An update is code that the
 * app runs, and nothing signs it: it must not travel over a network anyone could tamper with.
 */
export function isAllowedUpdateUrl(address: string): boolean {
  const url = URL.parse(address);
  if (url === null) {
    return false;
  }
  // A password or a token in the address would be written in clear in the package and in its command line.
  const carriesSecrets = url.username !== '' || url.password !== '' || url.search !== '' || url.hash !== '';
  const isSafeScheme = url.protocol === 'https:' || (url.protocol === 'http:' && LOOPBACK_HOSTS.has(url.hostname));
  return isSafeScheme && !carriesSecrets;
}

/** Replaces the update server in a message, so that the log never says where a package updates from. */
export function hideAddress(message: string, address: string | undefined): string {
  const url = address === undefined ? null : URL.parse(address);
  if (address === undefined || url === null) {
    return message;
  }
  // Errors name the whole address, without its last slash, or only the server: the longest comes first.
  const forms = [address, address.replace(/\/$/, ''), url.host].sort((a, b) => b.length - a.length);
  return forms.reduce((text, form) => text.replaceAll(form, '<update server>'), message);
}

/** Reads the update server from the value of the package.json setting. */
export function chooseUpdateSource(configured: unknown): UpdateSourceChoice {
  if (configured === undefined || configured === '') {
    return { kind: 'repository' };
  }
  if (typeof configured !== 'string') {
    return { kind: 'invalid', reason: `${UPDATE_SOURCE_SETTING} is not text.` };
  }
  if (!isAllowedUpdateUrl(configured)) {
    return {
      kind: 'invalid',
      reason: `${UPDATE_SOURCE_SETTING} must be ${UPDATE_SOURCE_REQUIREMENT}.`,
    };
  }
  return { kind: 'http', url: configured };
}
