import { describe, expect, it, vi } from 'vitest';

import { handleServerApiRequest, parseServerCall, type ServerApiOptions } from './serverApi';

const SERVER = { id: 'development', url: 'http://localhost:5180' };
const DEVELOPMENT_ORIGIN = 'http://localhost:5173';
const GET_SERVICE_INFO = 'voyager.diagnostics.v1.DiagnosticsService/GetServiceInfo';

function options(fetch: ServerApiOptions['fetch'] = vi.fn<ServerApiOptions['fetch']>()): ServerApiOptions {
  return {
    servers: () => [SERVER],
    app: { name: 'desktop', version: '0.4.0-beta.2' },
    allowedOrigins: [DEVELOPMENT_ORIGIN],
    fetch,
    log: { warn: vi.fn() },
  };
}

function call(path: string, init: RequestInit = {}): Request {
  return new Request(`voyager://app${path}`, {
    method: 'POST',
    body: new Uint8Array([0, 0, 0, 0, 0]),
    headers: { 'content-type': 'application/grpc-web+proto', 'x-grpc-web': '1', cookie: 'secret=1' },
    ...init,
  });
}

describe('parseServerCall', () => {
  it('reads the server and the method', () => {
    expect(parseServerCall(`/api/development/${GET_SERVICE_INFO}`)).toEqual({
      serverId: 'development',
      method: GET_SERVICE_INFO,
    });
  });

  it.each(['/plugins/x', '/api/development', '/api/development/Service', '/api//Service/Method', '/api/a/b/c/d'])(
    'finds no call in %s',
    (path) => {
      expect(parseServerCall(path)).toBeUndefined();
    },
  );
});

describe('handleServerApiRequest', () => {
  it('forwards an allowed call to the server, with the app and its version, and nothing of the page', async () => {
    const fetch = vi.fn<ServerApiOptions['fetch']>().mockResolvedValue(
      new Response(new Uint8Array([1, 2, 3]), {
        status: 200,
        headers: { 'content-type': 'application/grpc-web', 'set-cookie': 'tracking=1' },
      }),
    );

    const response = await handleServerApiRequest(call(`/api/development/${GET_SERVICE_INFO}`), options(fetch));

    const [url, init] = fetch.mock.calls[0] ?? [];
    expect(url).toBe(`http://localhost:5180/${GET_SERVICE_INFO}`);
    const headers = new Headers(init?.headers);
    expect(headers.get('x-voyager-app')).toBe('desktop');
    expect(headers.get('x-voyager-app-version')).toBe('0.4.0-beta.2');
    expect(headers.get('content-type')).toBe('application/grpc-web+proto');
    expect(headers.get('cookie')).toBeNull();
    expect(response.headers.get('content-type')).toBe('application/grpc-web');
    expect(response.headers.get('set-cookie')).toBeNull();
    expect(new Uint8Array(await response.arrayBuffer())).toEqual(new Uint8Array([1, 2, 3]));
  });

  it.each([
    ['a method the page may not call', `/api/development/voyager.pluginhost.v1.PluginHostService/ExaminePackage`],
    ['a server the app does not know', `/api/elsewhere/${GET_SERVICE_INFO}`],
  ])('refuses %s, without calling any server', async (_case, path) => {
    const fetch = vi.fn<ServerApiOptions['fetch']>();
    const settings = options(fetch);

    const response = await handleServerApiRequest(call(path), settings);

    expect(fetch).not.toHaveBeenCalled();
    expect(response.headers.get('grpc-status')).toBe('7');
    expect(settings.log.warn).toHaveBeenCalled();
  });

  it('refuses a call that is not a POST', async () => {
    const response = await handleServerApiRequest(
      call(`/api/development/${GET_SERVICE_INFO}`, { method: 'GET', body: null }),
      options(),
    );

    expect(response.headers.get('grpc-status')).toBe('7');
  });

  it('answers "unavailable" when the server does not answer, without its address', async () => {
    const fetch = vi
      .fn<ServerApiOptions['fetch']>()
      .mockRejectedValue(new Error('connect ECONNREFUSED 127.0.0.1:5180'));
    const settings = options(fetch);

    const response = await handleServerApiRequest(call(`/api/development/${GET_SERVICE_INFO}`), settings);

    expect(response.headers.get('grpc-status')).toBe('14');
    expect(decodeURIComponent(response.headers.get('grpc-message') ?? '')).not.toContain('127.0.0.1');
    expect(vi.mocked(settings.log.warn).mock.calls.flat().join(' ')).not.toContain('127.0.0.1');
  });

  it('lets the development page of another origin call and read the gRPC status', async () => {
    const fetch = vi
      .fn<ServerApiOptions['fetch']>()
      .mockResolvedValue(new Response(null, { status: 200, headers: { 'grpc-status': '0' } }));

    const response = await handleServerApiRequest(
      call(`/api/development/${GET_SERVICE_INFO}`, { headers: { origin: DEVELOPMENT_ORIGIN } }),
      options(fetch),
    );

    expect(response.headers.get('access-control-allow-origin')).toBe(DEVELOPMENT_ORIGIN);
    expect(response.headers.get('access-control-expose-headers')).toContain('grpc-status');
  });

  it('gives no CORS header to an origin it does not know', async () => {
    const response = await handleServerApiRequest(
      new Request(`voyager://app/api/development/${GET_SERVICE_INFO}`, {
        method: 'OPTIONS',
        headers: { origin: 'https://elsewhere.example.org' },
      }),
      options(),
    );

    expect(response.status).toBe(204);
    expect(response.headers.get('access-control-allow-origin')).toBeNull();
  });

  it('never follows a redirection nor sends cookies, and passes the body as the page sent it', async () => {
    const fetch = vi.fn<ServerApiOptions['fetch']>().mockResolvedValue(new Response(null, { status: 200 }));

    await handleServerApiRequest(call(`/api/development/${GET_SERVICE_INFO}`), options(fetch));

    const init = fetch.mock.calls[0]?.[1];
    expect(init?.redirect).toBe('error');
    expect(init?.credentials).toBe('omit');
    expect(new Uint8Array(init?.body as ArrayBuffer)).toEqual(new Uint8Array([0, 0, 0, 0, 0]));
  });

  it('relays the HTTP status of the server as it is', async () => {
    const fetch = vi.fn<ServerApiOptions['fetch']>().mockResolvedValue(new Response(null, { status: 503 }));

    const response = await handleServerApiRequest(call(`/api/development/${GET_SERVICE_INFO}`), options(fetch));

    expect(response.status).toBe(503);
  });

  it('refuses a badly encoded address as any refused call', async () => {
    const response = await handleServerApiRequest(call('/api/%E0%A4%A/x/y'), options());

    expect(response.headers.get('grpc-status')).toBe('7');
  });

  it('refuses a call from an origin it does not know, without calling the server', async () => {
    const fetch = vi.fn<ServerApiOptions['fetch']>();

    const response = await handleServerApiRequest(
      call(`/api/development/${GET_SERVICE_INFO}`, { headers: { origin: 'https://elsewhere.example.org' } }),
      options(fetch),
    );

    expect(fetch).not.toHaveBeenCalled();
    expect(response.headers.get('grpc-status')).toBe('7');
  });

  it('writes nothing the page chose to the log', async () => {
    const settings = options();

    await handleServerApiRequest(call('/api/made-up%0AFAKE%20LINE/Some.Service/Steal'), settings);

    const logged = vi.mocked(settings.log.warn).mock.calls.flat().join(' ');
    expect(logged).not.toContain('FAKE');
    expect(logged).not.toContain('Steal');
    expect(logged).toContain('unknown');
  });
});
