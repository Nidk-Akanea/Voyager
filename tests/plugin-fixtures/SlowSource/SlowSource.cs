using Voyager.Sdk;
using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginFixtures.SlowSource;

public sealed class SlowSourcePlugin : IPlugin
{
    public void Configure(IPluginBuilder builder) => builder.AddDiagnosticFactSource<SlowSource>();
}

// Never answers and ignores cancellation: the worst case for the host.
public sealed class SlowSource : IDiagnosticFactSource
{
    public async Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite);
        return [];
    }
}
