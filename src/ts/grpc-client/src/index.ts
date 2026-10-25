// Typed clients for the Voyager services, generated from proto/ (docs/conventions/grpc.md).
import { createClient, type Client, type Interceptor } from '@connectrpc/connect';
import { createGrpcWebTransport } from '@connectrpc/connect-web';

import { DiagnosticsService } from './gen/voyager/diagnostics/v1/diagnostics_pb.js';

export * from './gen/voyager/diagnostics/v1/diagnostics_pb.js';

/**
 * Header carrying the secret shared by the desktop app and its local service. Must match
 * LocalTokenInterceptor.TOKEN_HEADER in src/dotnet/Voyager.LocalService/Api/LocalTokenInterceptor.cs.
 */
export const LOCAL_TOKEN_HEADER = 'x-voyager-token';

/** Where the local service listens, and the secret it expects on every call. */
export interface LocalServiceConnection {
  readonly url: string;
  readonly token: string;
}

/** Clients for every service exposed by the local service. */
export interface LocalServiceClients {
  readonly diagnostics: Client<typeof DiagnosticsService>;
}

/** Creates the gRPC-Web clients of the local service (ADR-0003). */
export function createLocalServiceClients(connection: LocalServiceConnection): LocalServiceClients {
  const addToken: Interceptor = (next) => (request) => {
    request.header.set(LOCAL_TOKEN_HEADER, connection.token);
    return next(request);
  };

  const transport = createGrpcWebTransport({
    baseUrl: connection.url,
    interceptors: [addToken],
  });

  return {
    diagnostics: createClient(DiagnosticsService, transport),
  };
}
