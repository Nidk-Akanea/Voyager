// Forwards the page's calls to a group server (ADR-0019, decision 4): the page calls voyager://app/api/<server>/…, the
// main process checks the server and the service, adds who is calling, and sends the call to the server. The page never
// knows the address of the server, and never holds a token (the real sign-in comes later; in development the server
// takes the development identity).
import type { ServerEntry } from './serverRegistry';

/** The path, under the app's protocol, of the calls forwarded to a server. */
export const SERVER_API_PATH_PREFIX = '/api/';

/**
 * The gRPC methods the page may call on a server, as `<package>.<Service>/<Method>`. Anything else is refused: a module
 * of a plugin runs in the page, and the list is what limits what it can ask in the user's name.
 */
export const ALLOWED_METHODS: ReadonlySet<string> = new Set([
  'voyager.diagnostics.v1.DiagnosticsService/GetServiceInfo',
  'voyager.pluginhost.v1.PluginHostService/ListPlugins',
  'voyager.pluginhost.v1.PluginHostService/GetDiagnosticFacts',
]);

// The headers of a gRPC-Web call that go to the server; the rest (cookies, origin) stays here.
const FORWARDED_REQUEST_HEADERS = ['content-type', 'x-grpc-web', 'grpc-timeout', 'x-user-agent'];
// What a client of another origin (the Vite page, in development) may read of the answer.
const EXPOSED_RESPONSE_HEADERS = 'grpc-status, grpc-message, grpc-status-details-bin';
const FORWARD_TIMEOUT_MS = 30_000;

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

/** Reads `/api/<server>/<package.Service>/<Method>`; undefined for any other path. */
export function parseServerCall(pathname: string): ServerCall | undefined {
  if (!pathname.startsWith(SERVER_API_PATH_PREFIX)) {
    return undefined;
  }
  const [serverId, service, method, ...rest] = pathname
    .slice(SERVER_API_PATH_PREFIX.length)
    .split('/')
    .map((segment) => decodeURIComponent(segment));
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

/** Answers a call of the page under {@link SERVER_API_PATH_PREFIX}. */
export async function handleServerApiRequest(request: Request, options: ServerApiOptions): Promise<Response> {
  const cors = corsHeaders(request.headers.get('origin'), options.allowedOrigins);

  if (request.method === 'OPTIONS') {
    cors.set('Access-Control-Allow-Methods', 'POST');
    cors.set('Access-Control-Allow-Headers', FORWARDED_REQUEST_HEADERS.join(', '));
    cors.set('Access-Control-Max-Age', '600');
    return new Response(null, { status: 204, headers: cors });
  }

  const call = parseServerCall(new URL(request.url).pathname);
  const server = call === undefined ? undefined : options.servers().find((entry) => entry.id === call.serverId);
  if (request.method !== 'POST' || call === undefined || server === undefined || !ALLOWED_METHODS.has(call.method)) {
    // The method and the server named by the page; never a body, never an address.
    options.log.warn(
      `A call of the page was refused: ${request.method} ${call?.method ?? 'unknown method'} on server ${call?.serverId ?? 'unknown'}.`,
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
      signal: AbortSignal.timeout(FORWARD_TIMEOUT_MS),
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
