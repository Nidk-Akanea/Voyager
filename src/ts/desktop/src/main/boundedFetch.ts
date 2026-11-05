// A fetch whose responses cannot exceed a size: what a server sends the main process is read into memory, and a server
// that lies about the size of a file must not be able to fill it.

/** Wraps `fetch` so that a response larger than `maximumBytes` fails while it is read. */
export function boundedFetch(fetch: typeof globalThis.fetch, maximumBytes: number): typeof globalThis.fetch {
  return async (input, init) => {
    const response = await fetch(input, init);
    const tooLarge = (): Error => new Error(`The response of the server is larger than ${String(maximumBytes)} bytes.`);
    if (Number(response.headers.get('Content-Length') ?? 0) > maximumBytes) {
      await response.body?.cancel();
      throw tooLarge();
    }
    if (response.body === null) {
      return response;
    }
    let received = 0;
    const body = response.body.pipeThrough(
      new TransformStream<Uint8Array, Uint8Array>({
        transform: (chunk, controller) => {
          received += chunk.byteLength;
          if (received > maximumBytes) {
            controller.error(tooLarge());
            return;
          }
          controller.enqueue(chunk);
        },
      }),
    );
    return new Response(body, { status: response.status, statusText: response.statusText, headers: response.headers });
  };
}
