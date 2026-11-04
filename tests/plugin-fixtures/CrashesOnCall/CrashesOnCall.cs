using System.Diagnostics;

using Voyager.Sdk;
using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginFixtures.CrashesOnCall;

public sealed class CrashesOnCallPlugin : IPlugin
{
    public void Configure(IPluginBuilder builder) => builder.AddDiagnosticFactSource<CrashingSource>();
}

// Loads well, then ends the whole process when asked for its facts, as a plugin that crashes the service during a call
// would. Only ever called in a process of its own (tests/Voyager.LocalService.Tests).
public sealed class CrashingSource : IDiagnosticFactSource
{
    public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken)
    {
        Process.GetCurrentProcess().Kill();
        return Task.FromResult<IReadOnlyList<DiagnosticFact>>([]);
    }
}
