using Voyager.Sdk;
using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginFixtures.ManyFacts;

public sealed class ManyFactsPlugin : IPlugin
{
    public void Configure(IPluginBuilder builder) => builder.AddDiagnosticFactSource<ManyFactsSource>();
}

public sealed class ManyFactsSource : IDiagnosticFactSource
{
    public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DiagnosticFact>>(
            [.. Enumerable.Range(1, 60).Select(number => new DiagnosticFact($"fixture-many-facts-{number}", DiagnosticFactValue.FromNumber(number)))]);
}
