using System.Security.Cryptography;
using System.Text;

using Grpc.Core;
using Grpc.Core.Interceptors;

using Microsoft.Extensions.Options;

namespace Voyager.LocalService.Api;

/// <summary>
/// Refuses every gRPC call that does not carry the local token shared with the desktop app. The local
/// service only listens on the loopback interface, but other programs of the same machine could still
/// reach it: the token keeps the member's data for the desktop app alone.
/// </summary>
/// <param name="options">The local service settings, holding the expected token.</param>
/// <param name="logger">Records refused calls, without the token.</param>
public sealed partial class LocalTokenInterceptor(IOptions<LocalServiceOptions> options, ILogger<LocalTokenInterceptor> logger)
    : Interceptor
{
    #region Fields

    private readonly byte[] _expectedToken = Encoding.UTF8.GetBytes(options.Value.LocalToken);

    /// <summary>Header carrying the local token (same name in the TypeScript client).</summary>
    public const string TOKEN_HEADER = "x-voyager-token";

    #endregion Fields

    #region Private Methods

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refused a call to {Method}: missing or invalid local token")]
    private static partial void LogRejectedCall(ILogger logger, string method);

    private void EnsureAuthorized(ServerCallContext context)
    {
        var presented = context.RequestHeaders.GetValue(TOKEN_HEADER);

        // Constant-time comparison: the time taken must not reveal how much of the token was right.
        if (presented is not null
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), _expectedToken))
        {
            return;
        }

        LogRejectedCall(logger, context.Method);
        throw new RpcException(new Status(StatusCode.Unauthenticated, "A valid local token is required."));
    }

    #endregion Private Methods

    #region Public Methods

    /// <inheritdoc />
    public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        EnsureAuthorized(context);
        return continuation(requestStream, context);
    }

    /// <inheritdoc />
    public override Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        EnsureAuthorized(context);
        return continuation(requestStream, responseStream, context);
    }

    /// <inheritdoc />
    public override Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        EnsureAuthorized(context);
        return continuation(request, responseStream, context);
    }

    /// <inheritdoc />
    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        EnsureAuthorized(context);
        return continuation(request, context);
    }

    #endregion Public Methods
}
