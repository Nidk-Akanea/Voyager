using Microsoft.Extensions.DependencyInjection;

using Voyager.Sdk;
using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginFixtures.ThrowsOnDispose;

public sealed class ThrowsOnDisposePlugin : IPlugin
{
    public void Configure(IPluginBuilder builder)
    {
        builder.Services.AddSingleton<Resource>();
        builder.AddDiagnosticFactSource<ThrowsOnDisposeSource>();
    }
}

// A service of the plugin that fails when the container shuts down.
public sealed class Resource : IDisposable
{
    public void Dispose() => throw new InvalidOperationException("The resource cannot be released.");
}

public sealed class ThrowsOnDisposeSource(Resource resource) : IDiagnosticFactSource
{
    public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DiagnosticFact>>(
            [new("fixture-throws-on-dispose-ok", DiagnosticFactValue.FromText(resource.GetType().Name))]);
}
