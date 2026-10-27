using NodaTime;

using Voyager.Sdk;
using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginFixtures.Good;

public sealed class GoodPlugin : IPlugin
{
    public void Configure(IPluginBuilder builder) => builder.AddDiagnosticFactSource<GoodSource>();
}

public sealed class GoodSource : IDiagnosticFactSource
{
    public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DiagnosticFact>>(
        [
            new("fixture-good-text", DiagnosticFactValue.FromText("hello")),
            new("fixture-good-number", DiagnosticFactValue.FromNumber(42.5)),
            new("fixture-good-instant", DiagnosticFactValue.FromInstant(Instant.FromUtc(2026, 3, 14, 9, 30))),
        ]);
}
