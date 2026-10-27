using Voyager.Sdk;
using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginFixtures.ThrowingSource;

public sealed class ThrowingSourcePlugin : IPlugin
{
    public void Configure(IPluginBuilder builder) => builder.AddDiagnosticFactSource<ThrowingSource>();
}

public sealed class ThrowingSource : IDiagnosticFactSource
{
    public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The source is broken.");
}
