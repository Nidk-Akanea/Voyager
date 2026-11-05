import { describe, expect, it } from 'vitest';

import { boundedFetch } from './boundedFetch';

/** A server that answers `size` bytes, in chunks of 1 KiB, saying `declared` in Content-Length when given. */
function serverOf(size: number, declared?: number): typeof globalThis.fetch {
  return () => {
    let sent = 0;
    const body = new ReadableStream<Uint8Array>({
      pull: (controller) => {
        if (sent >= size) {
          controller.close();
          return;
        }
        const chunk = new Uint8Array(Math.min(1024, size - sent));
        sent += chunk.byteLength;
        controller.enqueue(chunk);
      },
    });
    const headers = declared === undefined ? undefined : { 'Content-Length': String(declared) };
    return Promise.resolve(new Response(body, { headers }));
  };
}

describe('boundedFetch', () => {
  it('lets a response within the size through, whole', async () => {
    const response = await boundedFetch(serverOf(3000), 4096)('https://family.example.org');

    expect((await response.arrayBuffer()).byteLength).toBe(3000);
  });

  it('fails while reading a response that goes beyond the size, whatever the server announced', async () => {
    const response = await boundedFetch(serverOf(10_000, 100), 4096)('https://family.example.org');

    await expect(response.arrayBuffer()).rejects.toThrow('larger than 4096 bytes');
  });

  it('refuses at once a response that announces more than the size', async () => {
    await expect(boundedFetch(serverOf(10_000, 10_000), 4096)('https://family.example.org')).rejects.toThrow(
      'larger than 4096 bytes',
    );
  });
});
