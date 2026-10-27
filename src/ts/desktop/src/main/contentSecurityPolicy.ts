// The Content Security Policy of the user interface. The page holds one inline script, the import map that lets
// the plugins share the page's own React (ADR-0015): Chromium accepts no external import map, so the policy
// allows exactly that script, by its hash, and no other inline script.
import { createHash } from 'node:crypto';

const IMPORT_MAP = /<script\s+type="importmap">([\s\S]*?)<\/script>/;

/** The `sha256-…` source of the import map of a page, or undefined when the page has none. */
export function findImportMapSource(html: string): string | undefined {
  const content = IMPORT_MAP.exec(html)?.[1];
  if (content === undefined) {
    return undefined;
  }
  return `'sha256-${createHash('sha256').update(content, 'utf8').digest('base64')}'`;
}

/** The policy of the user interface; `importMapSource` is what `findImportMapSource` found in its page. */
export function buildContentSecurityPolicy(importMapSource: string | undefined): string {
  return [
    "default-src 'self'",
    ["script-src 'self'", importMapSource].filter((source) => source !== undefined).join(' '),
    // Fluent UI (Griffel) inserts its styles while the page runs.
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data:",
    // The page asks for the translations of the plugins from its own protocol; the rest goes to the local service.
    "connect-src 'self' http://127.0.0.1:*",
    "object-src 'none'",
    "base-uri 'none'",
    "form-action 'none'",
    "frame-ancestors 'none'",
  ].join('; ');
}
