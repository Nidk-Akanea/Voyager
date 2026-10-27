using Voyager.Sdk;
using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginFixtures.UndeclaredRegistration;

// Registers a source although its manifest announces nothing.
public sealed class UndeclaredRegistrationPlugin : IPlugin
{
    public void Configure(IPluginBuilder builder) => builder.AddDiagnosticFactSource<UndeclaredSource>();
}

public sealed class UndeclaredSource : IDiagnosticFactSource
{
    public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DiagnosticFact>>([]);
}
