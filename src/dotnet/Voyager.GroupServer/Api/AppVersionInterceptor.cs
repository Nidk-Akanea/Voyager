using Grpc.Core;
using Grpc.Core.Interceptors;

using Microsoft.Extensions.Options;

namespace Voyager.GroupServer.Api;

/// <summary>
/// Refuses the calls of a desktop app older than <see cref="GroupServerOptions.MinimumAppVersion"/>, with a clear
/// message (ADR-0018, decision 6). The app gives its version in <see cref="VERSION_HEADER"/> on every call. Without a
/// minimum, every call is served, with or without the header.
/// </summary>
/// <param name="options">The server settings, holding the minimum version.</param>
/// <param name="logger">Records refused calls, with the version the app gave.</param>
public sealed partial class AppVersionInterceptor(IOptions<GroupServerOptions> options, ILogger<AppVersionInterceptor> logger)
    : Interceptor
{
    #region Fields

    /// <summary>The header in which the desktop app gives its version, such as <c>0.4.0-beta.2</c>.</summary>
    public const string VERSION_HEADER = "x-voyager-app-version";

    /// <summary>The message of a refused call; the app shows its own translated text for the status.</summary>
    public const string TOO_OLD_MESSAGE = "This version of Voyager is too old for this server: update it.";

    private readonly Version? _minimum = ParseCore(options.Value.MinimumAppVersion);

    #endregion Fields

    #region Private Methods

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refused a call to {Method}: the app gave the version '{AppVersion}', older than {MinimumVersion} or unreadable")]
    private static partial void LogRefusedCall(ILogger logger, string method, string appVersion, Version minimumVersion);

    // Major, minor and patch only: "0.4.0-beta.2" counts as 0.4.0. Null when the text is no version.
    private static Version? ParseCore(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var core = text.Trim();
        var suffix = core.IndexOfAny(['-', '+']);
        if (suffix >= 0)
        {
            core = core[..suffix];
        }

        return Version.TryParse(core, out var version) && version.Build >= 0 && version.Revision < 0 ? version : null;
    }

    private void EnsureRecentEnough(ServerCallContext context)
    {
        if (_minimum is null)
        {
            return;
        }

        var presented = context.RequestHeaders.GetValue(VERSION_HEADER) ?? string.Empty;
        if (ParseCore(presented) is { } version && version >= _minimum)
        {
            return;
        }

        // A version is no personal data; shortened, so that a strange header cannot flood the log.
        LogRefusedCall(logger, context.Method, presented.Length > 64 ? presented[..64] : presented, _minimum);
        throw new RpcException(new Status(StatusCode.FailedPrecondition, TOO_OLD_MESSAGE));
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
