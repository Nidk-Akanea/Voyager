using System.Runtime.Loader;

using Microsoft.Extensions.Logging;

using Voyager.Sdk;
using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginFixtures.Probe;

public sealed class ProbePlugin : IPlugin
{
    public void Configure(IPluginBuilder builder) => builder.AddDiagnosticFactSource<ProbeSource>();
}

// Reports where its own types and the shared SDK were loaded, and uses the logger of its private container.
public sealed class ProbeSource(ILogger<ProbeSource> logger) : IDiagnosticFactSource
{
    public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("The probe plugin was asked for its facts");
        var ownContext = AssemblyLoadContext.GetLoadContext(typeof(ProbeSource).Assembly)!;
        var sdkContext = AssemblyLoadContext.GetLoadContext(typeof(IPlugin).Assembly)!;
        return Task.FromResult<IReadOnlyList<DiagnosticFact>>(
        [
            new("fixture-probe-own-context", DiagnosticFactValue.FromText(ownContext.Name ?? string.Empty)),
            new("fixture-probe-sdk-context", DiagnosticFactValue.FromText(sdkContext.Name ?? string.Empty)),
        ]);
    }
}
