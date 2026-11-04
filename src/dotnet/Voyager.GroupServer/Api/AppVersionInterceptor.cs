using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;

using Grpc.Core;
using Grpc.Core.Interceptors;

using Microsoft.Extensions.Options;

using GrpcStatus = Grpc.Core.Status;
using RpcStatus = Google.Rpc.Status;

namespace Voyager.GroupServer.Api;

/// <summary>
/// Refuses the calls of a desktop app older than <see cref="GroupServerOptions.MinimumAppVersion"/>, with a stable
/// reason the app translates (ADR-0018, decision 6). The app names itself in <see cref="APP_HEADER"/> and gives its
/// version in <see cref="VERSION_HEADER"/> on every call (docs/conventions/grpc.md). Without a minimum, every call is
/// served, with or without the headers.
/// </summary>
/// <remarks>
/// It runs after authentication: once the real sign-in exists, an app that is too old and not signed in will be told
/// "unauthenticated" first. Checking the version before authentication comes with the sign-in (ADR-0019).
/// </remarks>
/// <param name="options">The server settings, holding the minimum version, checked at startup.</param>
/// <param name="logger">Records refused calls, with the app and the version it gave.</param>
public sealed partial class AppVersionInterceptor(IOptions<GroupServerOptions> options, ILogger<AppVersionInterceptor> logger)
    : Interceptor
{
    #region Fields

    /// <summary>The header in which the client names itself, such as <c>desktop</c>.</summary>
    public const string APP_HEADER = "x-voyager-app";

    /// <summary>The header in which the client gives its version, such as <c>0.4.0-beta.2</c>.</summary>
    public const string VERSION_HEADER = "x-voyager-app-version";

    /// <summary>The stable reason of the refusal, in the <c>google.rpc.ErrorInfo</c> of the status.</summary>
    public const string TOO_OLD_REASON = "APP_VERSION_TOO_OLD";

    /// <summary>The domain of the reasons Voyager gives.</summary>
    public const string ERROR_DOMAIN = "voyager";

    /// <summary>The message of a refused call, for developers; the app shows its own translated text for the reason.</summary>
    public const string TOO_OLD_MESSAGE = "This version of Voyager is too old for this server: update it.";

    // The trailer that carries the google.rpc.Status with its details (the gRPC rich error model).
    private const string STATUS_DETAILS_TRAILER = "grpc-status-details-bin";

    // A header comes from the caller: shortened, so that a strange one cannot flood the log.
    private const int MAXIMUM_LOGGED_LENGTH = 64;

    private readonly Version? _minimum = AppVersion.ParseCore(options.Value.MinimumAppVersion);

    #endregion Fields

    #region Private Methods

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refused a call to {Method}: the app '{App}' gave the version '{AppVersion}', older than {MinimumVersion} or unreadable")]
    private static partial void LogRefusedCall(ILogger logger, string method, string app, string appVersion, Version minimumVersion);

    private static string Shorten(string? text) =>
        text is null ? string.Empty : text.Length > MAXIMUM_LOGGED_LENGTH ? text[..MAXIMUM_LOGGED_LENGTH] : text;

    private static RpcException TooOld()
    {
        var details = new RpcStatus
        {
            Code = (int)StatusCode.FailedPrecondition,
            Message = TOO_OLD_MESSAGE,
            Details = { Any.Pack(new ErrorInfo { Reason = TOO_OLD_REASON, Domain = ERROR_DOMAIN }) },
        };

        return new RpcException(
            new GrpcStatus(StatusCode.FailedPrecondition, TOO_OLD_MESSAGE),
            new Metadata { { STATUS_DETAILS_TRAILER, details.ToByteArray() } });
    }

    private void EnsureRecentEnough(ServerCallContext context)
    {
        if (_minimum is null)
        {
            return;
        }

        var presented = context.RequestHeaders.GetValue(VERSION_HEADER);
        if (AppVersion.ParseCore(presented) is { } version && version >= _minimum)
        {
            return;
        }

        // An app name and a version are no personal data.
        LogRefusedCall(logger, context.Method, Shorten(context.RequestHeaders.GetValue(APP_HEADER)), Shorten(presented), _minimum);
        throw TooOld();
    }

    #endregion Private Methods

    #region Public Methods

    /// <inheritdoc />
    public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        EnsureRecentEnough(context);
        return continuation(requestStream, context);
    }

    /// <inheritdoc />
    public override Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        EnsureRecentEnough(context);
        return continuation(requestStream, responseStream, context);
    }

    /// <inheritdoc />
    public override Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        EnsureRecentEnough(context);
        return continuation(request, responseStream, context);
    }

    /// <inheritdoc />
    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        EnsureRecentEnough(context);
        return continuation(request, context);
    }

    #endregion Public Methods
}
