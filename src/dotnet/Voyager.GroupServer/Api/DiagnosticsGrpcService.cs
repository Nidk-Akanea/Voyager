using System.Runtime.InteropServices;

using Google.Protobuf.WellKnownTypes;

using Grpc.Core;

using Voyager.Contracts.Diagnostics.V1;

namespace Voyager.GroupServer.Api;

/// <summary>
/// Answers the "About" screen: version, runtime, environment, start time and database health of the group server
/// (proto/voyager/diagnostics/v1/diagnostics.proto).
/// </summary>
/// <remarks>
/// A technical service: it checks the database connection directly. Business services go through the use cases and
/// ports of Voyager.Core instead (ADR-0005).
/// </remarks>
/// <param name="serverInfo">Version and start time of this process.</param>
/// <param name="database">Tells whether the database answers.</param>
/// <param name="environment">The hosting environment.</param>
public sealed class DiagnosticsGrpcService(
    ServerInfo serverInfo,
    DatabaseHealth database,
    IHostEnvironment environment)
    : DiagnosticsService.DiagnosticsServiceBase
{
    #region Public Methods

    /// <inheritdoc />
    public override async Task<GetServiceInfoResponse> GetServiceInfo(GetServiceInfoRequest request, ServerCallContext context)
    {
        var databaseAvailable = await database.CanConnectAsync(context.CancellationToken);

        return new GetServiceInfoResponse
        {
            Version = serverInfo.Version,
            Runtime = RuntimeInformation.FrameworkDescription,
            Environment = environment.EnvironmentName,
            StartedAt = Timestamp.FromDateTimeOffset(serverInfo.StartedAt.ToDateTimeOffset()),
            DatabaseStatus = databaseAvailable ? DatabaseStatus.Available : DatabaseStatus.Unavailable,
        };
    }

    #endregion Public Methods
}
