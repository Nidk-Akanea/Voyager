// Forwards the page's calls to a group server (ADR-0019, decision 4): the page calls voyager://app/api/<server>/…, the
// main process checks the origin, the server and the method, adds who is calling, and sends the call to the server. The
// page never calls the server itself (it may show its address) and never holds a token: the real sign-in comes later,
// and in development the server takes the development identity.
import { DiagnosticsService, PluginHostService } from '@voyager/grpc-client';

import type { ServerEntry } from './serverRegistry';

/**
 * The path, under the app's protocol, of the calls forwarded to a server. Must match SERVER_API_BASE in
 * src/ts/grpc-client/src/servers.ts.
 */
export const SERVER_API_PATH_PREFIX = '/api/';

/** The origin of the app's own page. */
const APP_ORIGIN = 'voyager://app';

/** The part of a generated service this module reads. */
interface ServiceDescriptor {
  readonly typeName: string;
  readonly methods: readonly { readonly name: string }[];
}

function methodsOf(service: ServiceDescriptor, names: readonly string[]): string[] {
  return names.map((name) => {
    const method = service.methods.find((candidate) => candidate.name === name);
    if (method === undefined) {
      throw new Error(`The service ${service.typeName} has no method ${name}.`);
    }
    return `${service.typeName}/${method.name}`;
  });
}

/**
 * The gRPC methods the page may call on a server, as `<package>.<Service>/<Method>`, taken from the generated contracts.
 * Anything else is refused: a module of a plugin runs in the page, and this list is what limits what it can ask in the
 * user's name.
 */
export const ALLOWED_METHODS: ReadonlySet<string> = new Set([
  ...methodsOf(DiagnosticsService, ['GetServiceInfo']),
  ...methodsOf(PluginHostService, ['ListPlugins', 'GetDiagnosticFacts']),
]);

// The headers of a gRPC-Web call that go to the server; the rest (cookies, origin) stays here.
const FORWARDED_REQUEST_HEADERS = ['content-type', 'x-grpc-web', 'grpc-timeout', 'x-user-agent'];
// What a client of another origin (the Vite page, in development) may read of the answer.
const EXPOSED_RESPONSE_HEADERS = 'grpc-status, grpc-message, grpc-status-details-bin';
const FORWARD_TIMEOUT_MS = 30_000;
// What the page sent is written to the log only when it is a server or a method the app knows: anything else would let a
// module of a plugin write lines of its own there.
const UNKNOWN = 'unknown';

// gRPC status codes (https://grpc.github.io/grpc/core/md_doc_statuscodes.html).
const PERMISSION_DENIED = 7;
const UNAVAILABLE = 14;

/** Who calls: the headers the server reads to know the app and its version (docs/conventions/grpc.md). */
export interface CallingApp {
  readonly name: string;
  readonly version: string;
}

export interface ServerApiOptions {
  /** The servers the app knows. */
  readonly servers: () => readonly ServerEntry[];
  readonly app: CallingApp;
  /** The origins, besides the app's own, whose page may call (the Vite page in development). */
  readonly allowedOrigins: readonly string[];
  /** Sends a request to a server; Electron's `net.fetch` in the app, a fake in tests. */
  readonly fetch: (url: string, init: RequestInit) => Promise<Response>;
  readonly log: { readonly warn: (message: string) => void };
}

/** A call the page addressed to a server: which server, and which method. */
interface ServerCall {
  readonly serverId: string;
  readonly method: string;
}

/** Reads `/api/<server>/<package.Service>/<Method>`; undefined for any other path, a badly encoded one included. */
export function parseServerCall(pathname: string): ServerCall | undefined {
  if (!pathname.startsWith(SERVER_API_PATH_PREFIX)) {
    return undefined;
  }
  let segments: string[];
  try {
    segments = pathname
      .slice(SERVER_API_PATH_PREFIX.length)
      .split('/')
      .map((segment) => decodeURIComponent(segment));
  } catch {
    return undefined;
  }
  const [serverId, service, method, ...rest] = segments;
  if (serverId === undefined || service === undefined || method === undefined || rest.length > 0) {
    return undefined;
  }
  if (serverId === '' || service === '' || method === '') {
    return undefined;
  }
  return { serverId, method: `${service}/${method}` };
}

/** An answer that carries only a gRPC status, as gRPC-Web allows: the client reads it like any refusal. */
function grpcStatusResponse(code: number, message: string, cors: Headers): Response {
  const headers = new Headers(cors);
  headers.set('Content-Type', 'application/grpc-web+proto');
  headers.set('grpc-status', String(code));
  headers.set('grpc-message', encodeURIComponent(message));
  return new Response(null, { status: 200, headers });
}

/** The CORS headers for a page of another allowed origin; none for the app's own page. */
function corsHeaders(origin: string | null, allowedOrigins: readonly string[]): Headers {
  const headers = new Headers();
  if (origin !== null && allowedOrigins.includes(origin)) {
    headers.set('Access-Control-Allow-Origin', origin);
    headers.set('Access-Control-Expose-Headers', EXPOSED_RESPONSE_HEADERS);
    headers.set('Vary', 'Origin');
  }
  return headers;
}

/** Whether a request comes from the app's page, or from an allowed one; a request without an origin is the app's. */
function isAllowedOrigin(origin: string | null, allowedOrigins: readonly string[]): boolean {
  return origin === null || origin === APP_ORIGIN || allowedOrigins.includes(origin);
}

/** Answers a call of the page under {@link SERVER_API_PATH_PREFIX}. */
export async function handleServerApiRequest(request: Request, options: ServerApiOptions): Promise<Response> {
  const origin = request.headers.get('origin');
  const cors = corsHeaders(origin, options.allowedOrigins);

  if (request.method === 'OPTIONS') {
    cors.set('Access-Control-Allow-Methods', 'POST');
    cors.set('Access-Control-Allow-Headers', FORWARDED_REQUEST_HEADERS.join(', '));
    cors.set('Access-Control-Max-Age', '600');
    return new Response(null, { status: 204, headers: cors });
  }

  const call = parseServerCall(new URL(request.url).pathname);
  const server = call === undefined ? undefined : options.servers().find((entry) => entry.id === call.serverId);
  const allowedMethod = call !== undefined && ALLOWED_METHODS.has(call.method);
  if (
    request.method !== 'POST' ||
    !isAllowedOrigin(origin, options.allowedOrigins) ||
    server === undefined ||
    !allowedMethod
  ) {
    options.log.warn(
      `A call of the page was refused: ${request.method === 'POST' ? 'POST' : 'not a POST'} ` +
        `${allowedMethod ? call.method : UNKNOWN} on server ${server?.id ?? UNKNOWN}.`,
    );
    return grpcStatusResponse(PERMISSION_DENIED, 'This call is not allowed to the page.', cors);
  }

  const headers = new Headers();
  for (const name of FORWARDED_REQUEST_HEADERS) {
    const value = request.headers.get(name);
    if (value !== null) {
      headers.set(name, value);
    }
  }
  headers.set('x-voyager-app', options.app.name);
  headers.set('x-voyager-app-version', options.app.version);

  let answer: Response;
  try {
    answer = await options.fetch(`${server.url}/${call.method}`, {
      method: 'POST',
      headers,
      body: await request.arrayBuffer(),
      // A redirection would take the call elsewhere than the address checked when the server was registered.
      redirect: 'error',
      credentials: 'omit',
      // The page gave up (it left the screen): so does the call; and none waits forever.
      signal: AbortSignal.any([request.signal, AbortSignal.timeout(FORWARD_TIMEOUT_MS)]),
    });
  } catch {
    // The cause names the address of the server: it stays out of the page and of the log.
    options.log.warn(`The server ${server.id} did not answer a call to ${call.method}.`);
    return grpcStatusResponse(UNAVAILABLE, 'The server does not answer.', cors);
  }

  const responseHeaders = new Headers(cors);
  answer.headers.forEach((value, name) => {
    if (name === 'content-type' || name.startsWith('grpc-')) {
      responseHeaders.set(name, value);
    }
  });
  return new Response(answer.body, { status: answer.status, headers: responseHeaders });
}
