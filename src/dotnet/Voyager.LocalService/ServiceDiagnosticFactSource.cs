using System.Runtime.InteropServices;

using Voyager.Persistence;
using Voyager.Sdk.Diagnostics;

namespace Voyager.LocalService;

/// <summary>
/// The local service as a source of diagnostic facts: the same facts as the "About" screen, offered
/// through the extension point that plugins use (ADR-0015, decision 12). Having a second source than
/// the demonstration plugins is what makes the extension point real.
/// </summary>
/// <param name="serviceInfo">Version and start time of this process.</param>
/// <param name="databaseState">Whether the database was prepared at startup.</param>
/// <param name="scopeFactory">Gives the database connection, which lives in a request scope.</param>
/// <param name="environment">The hosting environment.</param>
public sealed class ServiceDiagnosticFactSource(
    ServiceInfo serviceInfo,
    LocalDatabaseState databaseState,
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment)
    : IDiagnosticFactSource
{
    #region Public Methods

    /// <inheritdoc />
    /// <remarks>
    /// Labels start with <c>service-</c>, the identifier the service provides under
    /// (<see cref="Voyager.PluginHost.PluginCatalog.SERVICE_ID"/>), like those of a plugin.
    /// </remarks>
    public async Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken)
    {
        var databaseAvailable = false;
        if (databaseState.IsReady)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<VoyagerDbContext>();
            databaseAvailable = await database.Database.CanConnectAsync(cancellationToken);
        }

        return
        [
            new("service-fact-version", DiagnosticFactValue.FromText(serviceInfo.Version)),
            new("service-fact-runtime", DiagnosticFactValue.FromText(RuntimeInformation.FrameworkDescription)),
            new("service-fact-environment", DiagnosticFactValue.FromText(environment.EnvironmentName)),
            new("service-fact-started-at", DiagnosticFactValue.FromInstant(serviceInfo.StartedAt)),
            new("service-fact-database", DiagnosticFactValue.FromText(databaseAvailable ? "available" : "unavailable")),
        ];
    }

    #endregion Public Methods
}
