using System.Runtime.InteropServices;

using Google.Protobuf.WellKnownTypes;

using Grpc.Core;

using Voyager.Contracts.Diagnostics.V1;
using Voyager.Persistence;

namespace Voyager.LocalService.Api;

/// <summary>
/// Answers the "About" screen: version, runtime, environment, start time and database health of the
/// local service (proto/voyager/diagnostics/v1/diagnostics.proto).
/// </summary>
/// <remarks>
/// A technical service: it reads the database connection directly. Business services go through the
/// use cases and ports of Voyager.Core instead (ADR-0005).
/// </remarks>
/// <param name="serviceInfo">Version and start time of this process.</param>
/// <param name="databaseState">Whether the database was prepared at startup.</param>
/// <param name="database">The local SQLite database.</param>
/// <param name="environment">The hosting environment.</param>
public sealed class DiagnosticsGrpcService(
    ServiceInfo serviceInfo,
    LocalDatabaseState databaseState,
    VoyagerDbContext database,
    IHostEnvironment environment)
    : DiagnosticsService.DiagnosticsServiceBase
{
    #region Public Methods

    /// <inheritdoc />
    public override async Task<GetServiceInfoResponse> GetServiceInfo(GetServiceInfoRequest request, ServerCallContext context)
    {
        var databaseAvailable = databaseState.IsReady
            && await database.Database.CanConnectAsync(context.CancellationToken);

        return new GetServiceInfoResponse
        {
            Version = serviceInfo.Version,
            Runtime = RuntimeInformation.FrameworkDescription,
            Environment = environment.EnvironmentName,
            StartedAt = Timestamp.FromDateTimeOffset(serviceInfo.StartedAt.ToDateTimeOffset()),
            DatabaseStatus = databaseAvailable ? DatabaseStatus.Available : DatabaseStatus.Unavailable,
        };
    }

    #endregion Public Methods
}
