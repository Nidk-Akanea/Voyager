// The group servers the desktop app talks to (ADR-0018), and what their refusals mean for the user interface.
import { Code, ConnectError, createClient, type Interceptor } from '@connectrpc/connect';
import { createGrpcWebTransport } from '@connectrpc/connect-web';

import { DiagnosticsService } from './gen/voyager/diagnostics/v1/diagnostics_pb.js';
import { PluginHostService } from './gen/voyager/pluginhost/v1/pluginhost_pb.js';
import type { VoyagerClients } from './index.js';

/**
 * A group server the desktop app knows (spec of the switch to client-server, "Les serveurs enregistrés"). The page names
 * a server by its identifier only; the main process holds its address and forwards the calls.
 */
export interface RegisteredServer {
  /** Stable identifier, in lower case letters, digits and dashes, such as `development`. */
  readonly id: string;
  /** The address of the server, shown to the user (the page never calls it directly). */
  readonly address: string;
}

/** The scheme, host and path of the internal protocol under which the main process forwards calls to a server. */
const SERVER_API_BASE = 'voyager://app/api/';

/**
 * The stable reason the server gives when it no longer serves this version of the app. Must match
 * AppVersionInterceptor.TOO_OLD_REASON in src/dotnet/Voyager.GroupServer/Api/AppVersionInterceptor.cs.
 */
export const APP_VERSION_TOO_OLD_REASON = 'APP_VERSION_TOO_OLD';

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
 */
export function createServerClientsAt(baseUrl: string, headers: Readonly<Record<string, string>>): VoyagerClients {
  const addHeaders: Interceptor = (next) => (request) => {
    for (const [name, value] of Object.entries(headers)) {
      request.header.set(name, value);
    }
    return next(request);
  };
  const transport = createGrpcWebTransport({ baseUrl, interceptors: [addHeaders] });
  return {
    diagnostics: createClient(DiagnosticsService, transport),
    pluginHost: createClient(PluginHostService, transport),
  };
}

/**
 * Why a call to a server failed, as the user interface tells it:
 * - `unreachable`: the server does not answer (stopped, network down, too slow);
 * - `app-too-old`: the server no longer serves this version of the app;
 * - `not-signed-in`: the server wants a signed-in user (the real sign-in comes later, ADR-0019);
 * - `failed`: any other refusal.
 */
export type ServerProblem = 'unreachable' | 'app-too-old' | 'not-signed-in' | 'failed';

const UNREACHABLE_CODES: ReadonlySet<Code> = new Set([
  Code.Unavailable,
  Code.DeadlineExceeded,
  Code.Unknown,
  Code.Canceled,
]);

/** Whether the details of an error hold a `google.rpc.ErrorInfo` with the given reason. */
function hasReason(error: ConnectError, reason: string): boolean {
  const encodedReason = new TextEncoder().encode(reason);
  return error.details.some((detail) => {
    if (!('type' in detail) || detail.type !== 'google.rpc.ErrorInfo') {
      return false;
    }
    // The reason is a string field of the message: its bytes appear as they are in the encoded details.
    return containsBytes(detail.value, encodedReason);
  });
}

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
