import { createHash } from 'node:crypto';

import { describe, expect, it } from 'vitest';

import { buildContentSecurityPolicy, findImportMapSource } from './contentSecurityPolicy';

const IMPORT_MAP = '{"imports":{"react":"./shared/react.js"}}';
const PAGE = `<!doctype html><html><head><script type="importmap">${IMPORT_MAP}</script></head><body></body></html>`;

describe('findImportMapSource', () => {
  it('hashes the exact content of the import map', () => {
    const hash = createHash('sha256').update(IMPORT_MAP).digest('base64');

    expect(findImportMapSource(PAGE)).toBe(`'sha256-${hash}'`);
  });

  it('changes when the import map changes', () => {
    expect(findImportMapSource(PAGE.replace('react.js', 'other.js'))).not.toBe(findImportMapSource(PAGE));
  });

  it('finds nothing in a page without an import map', () => {
    expect(findImportMapSource('<html><script type="module" src="/main.js"></script></html>')).toBeUndefined();
  });
});

describe('buildContentSecurityPolicy', () => {
  it('allows the import map by its hash, and no other inline script', () => {
    const policy = buildContentSecurityPolicy("'sha256-abc='");

    expect(policy).toContain("script-src 'self' 'sha256-abc='");
    expect(policy.split('; ').find((directive) => directive.startsWith('script-src'))).not.toContain('unsafe');
  });

  it('allows scripts of the page itself only when there is no import map', () => {
    expect(buildContentSecurityPolicy(undefined).split('; ')).toContain("script-src 'self'");
  });

  it('keeps the locked-down directives', () => {
    const directives = buildContentSecurityPolicy(undefined).split('; ');

    expect(directives).toEqual(
      expect.arrayContaining(["default-src 'self'", "object-src 'none'", "base-uri 'none'", "frame-ancestors 'none'"]),
    );
  });
});
