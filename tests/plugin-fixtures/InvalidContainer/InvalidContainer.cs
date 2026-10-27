using Voyager.Sdk;
using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginFixtures.InvalidContainer;

// Its source needs a service that nobody registers: the private container is invalid.
public sealed class InvalidContainerPlugin : IPlugin
{
    public void Configure(IPluginBuilder builder) => builder.AddDiagnosticFactSource<NeedsMissingServiceSource>();
}

public interface IMissingService;

public sealed class NeedsMissingServiceSource(IMissingService missing) : IDiagnosticFactSource
{
    public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DiagnosticFact>>(
            [new("fixture-invalid-container-x", DiagnosticFactValue.FromText(missing.ToString() ?? string.Empty))]);
}
