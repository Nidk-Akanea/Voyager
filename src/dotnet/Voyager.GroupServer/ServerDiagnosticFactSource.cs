using System.Runtime.InteropServices;

using Voyager.Sdk.Diagnostics;

namespace Voyager.GroupServer;

/// <summary>
/// The group server as a source of diagnostic facts: the same facts as the "About" screen, offered through the
/// extension point that plugins use (ADR-0015, decision 12). A source of the server, called without a group
/// (ADR-0020, decision 2).
/// </summary>
/// <param name="serverInfo">Version and start time of this process.</param>
/// <param name="database">Tells whether the database answers.</param>
/// <param name="environment">The hosting environment.</param>
public sealed class ServerDiagnosticFactSource(
    ServerInfo serverInfo,
    DatabaseHealth database,
    IHostEnvironment environment)
    : IDiagnosticFactSource
{
    #region Public Methods

    /// <inheritdoc />
    /// <remarks>
    /// Labels start with <c>service-</c>, the identifier the server provides under
    /// (<see cref="Voyager.PluginHost.PluginCatalog.SERVICE_ID"/>), like those of a plugin: the user interface already
    /// has their texts.
    /// </remarks>
    public async Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken)
    {
        var databaseAvailable = await database.CanConnectAsync(cancellationToken);

        return
        [
            new("service-fact-version", DiagnosticFactValue.FromText(serverInfo.Version)),
            new("service-fact-runtime", DiagnosticFactValue.FromText(RuntimeInformation.FrameworkDescription)),
            new("service-fact-environment", DiagnosticFactValue.FromText(environment.EnvironmentName)),
            new("service-fact-started-at", DiagnosticFactValue.FromInstant(serverInfo.StartedAt)),
            new("service-fact-database", DiagnosticFactValue.FromText(databaseAvailable ? "available" : "unavailable")),
        ];
    }

    #endregion Public Methods
}
