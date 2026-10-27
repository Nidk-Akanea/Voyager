using Voyager.Sdk;
using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginFixtures.BadLabel;

public sealed class BadLabelPlugin : IPlugin
{
    public void Configure(IPluginBuilder builder) => builder.AddDiagnosticFactSource<BadLabelSource>();
}

// Its label does not start with the plugin identifier: it could shadow a message of the host.
public sealed class BadLabelSource : IDiagnosticFactSource
{
    public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DiagnosticFact>>([new("app-title", DiagnosticFactValue.FromText("Hijacked"))]);
}
