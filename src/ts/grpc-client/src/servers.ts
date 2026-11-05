// The group servers the desktop app talks to (ADR-0018), and what their refusals mean for the user interface.
import { Code, ConnectError, createClient, type Interceptor } from '@connectrpc/connect';
import { createGrpcWebTransport } from '@connectrpc/connect-web';

import { DiagnosticsService } from './gen/voyager/diagnostics/v1/diagnostics_pb.js';
import { PluginHostService } from './gen/voyager/pluginhost/v1/pluginhost_pb.js';
import type { VoyagerClients } from './index.js';

/**
 * A group server the desktop app knows (spec of the switch to client-server, "Les serveurs enregistrés"). The page calls
 * a server by its identifier only, through the main process, which forwards the calls; it may show its address.
 */
export interface RegisteredServer {
  /** Stable identifier, in lower case letters, digits and dashes, such as `development`. */
  readonly id: string;
  /** The address of the server, shown to the user; the page never calls it directly. */
  readonly address: string;
}

/**
 * Another server the app knows, as a view sees it: its identifier only, for the selector; its address stays with the
 * main process and the view of that server.
 */
export interface ServerChoice {
  readonly id: string;
}

/**
 * The scheme, host and path of the internal protocol under which the main process forwards calls to a server. Must match
 * SERVER_API_PATH_PREFIX in src/ts/desktop/src/main/serverApi.ts.
 */
const SERVER_API_BASE = 'voyager://app/api/';

/**
 * The stable reason the server gives when it no longer serves this version of the app. Must match
 * AppVersionInterceptor.TOO_OLD_REASON in src/dotnet/Voyager.GroupServer/Api/AppVersionInterceptor.cs.
 */
export const APP_VERSION_TOO_OLD_REASON = 'APP_VERSION_TOO_OLD';

/** The domain of the reasons Voyager gives, in the `google.rpc.ErrorInfo` of a refusal (docs/conventions/grpc.md). */
const VOYAGER_ERROR_DOMAIN = 'voyager';

/** The address, inside the app, of a registered server: the main process forwards what is sent there (ADR-0019). */
export function serverApiBaseUrl(serverId: string): string {
  return `${SERVER_API_BASE}${encodeURIComponent(serverId)}`;
}

/** Creates the gRPC-Web clients of a registered server, through the main process: no token, no address. */
export function createServerClients(serverId: string): VoyagerClients {
  return createServerClientsAt(serverApiBaseUrl(serverId), {});
}

/**
 * Creates the gRPC-Web clients of a server at its own address, for the main process only: the page always goes through
 * {@link createServerClients}.
 * @param headers Added to every call, such as the name and version of the app.
 * @param fetch Sends the calls; Electron's `net.fetch` in the main process, so that the proxy and the certificates of the
 * system apply, as for the calls of the page.
 */
export function createServerClientsAt(
  baseUrl: string,
  headers: Readonly<Record<string, string>>,
  fetch?: typeof globalThis.fetch,
): VoyagerClients {
  const addHeaders: Interceptor = (next) => (request) => {
    for (const [name, value] of Object.entries(headers)) {
      request.header.set(name, value);
    }
    return next(request);
  };
  const transport = createGrpcWebTransport({ baseUrl, interceptors: [addHeaders], fetch });
  return {
    diagnostics: createClient(DiagnosticsService, transport),
    pluginHost: createClient(PluginHostService, transport),
  };
}

/**
 * Why a call to a server failed, as the user interface tells it:
 * - `unreachable`: the server does not answer (stopped, network down, too slow): a passing problem, tried again;
 * - `app-too-old`: the server no longer serves this version of the app;
 * - `not-signed-in`: the server wants a signed-in user (the real sign-in comes later, ADR-0019);
 * - `failed`: any other refusal, or an error of the server itself.
 */
export type ServerProblem = 'unreachable' | 'app-too-old' | 'not-signed-in' | 'failed';

// A server that answered with an error of its own (Unknown, Internal) has answered: that is no unreachable server.
const UNREACHABLE_CODES: ReadonlySet<Code> = new Set([Code.Unavailable, Code.DeadlineExceeded]);

function containsBytes(haystack: Uint8Array, needle: Uint8Array): boolean {
  outer: for (let start = 0; start + needle.length <= haystack.length; start++) {
    for (let index = 0; index < needle.length; index++) {
      if (haystack[start + index] !== needle[index]) {
        continue outer;
      }
    }
    return true;
  }
  return false;
}

/**
 * Whether the details of an error hold a `google.rpc.ErrorInfo` of Voyager with the given reason. The reason and the
 * domain are string fields of the message, so their bytes appear as they are in the encoded details; decoding the message
 * itself comes when the Google types join the contracts (docs/conventions/grpc.md).
 */
function hasReason(error: ConnectError, reason: string): boolean {
  const encoder = new TextEncoder();
  return error.details.some(
    (detail) =>
      'type' in detail &&
      detail.type === 'google.rpc.ErrorInfo' &&
      containsBytes(detail.value, encoder.encode(reason)) &&
      containsBytes(detail.value, encoder.encode(VOYAGER_ERROR_DOMAIN)),
  );
}

/** What a failed call to a server means for the user (see {@link ServerProblem}). */
export function describeServerProblem(error: unknown): ServerProblem {
  const connectError = ConnectError.from(error);
  if (connectError.code === Code.FailedPrecondition && hasReason(connectError, APP_VERSION_TOO_OLD_REASON)) {
    return 'app-too-old';
  }
  if (connectError.code === Code.Unauthenticated) {
    return 'not-signed-in';
  }
  return UNREACHABLE_CODES.has(connectError.code) ? 'unreachable' : 'failed';
}
