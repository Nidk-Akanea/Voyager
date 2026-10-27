using System.Diagnostics;

using Voyager.Sdk.Diagnostics;

namespace Voyager.Plugins.DiagnosticsRuntime;

/// <summary>
/// Facts about the running service that say nothing about its user: how many processors it sees, how much
/// memory it holds and how often the garbage collector ran. No name, no path.
/// </summary>
public sealed class RuntimeFactSource : IDiagnosticFactSource
{
    #region Fields

    private const double BYTES_PER_MEGABYTE = 1024 * 1024;

    #endregion Fields

    #region Public Methods

    /// <inheritdoc />
    public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken)
    {
        using var process = Process.GetCurrentProcess();
        var memoryMegabytes = Math.Round(process.WorkingSet64 / BYTES_PER_MEGABYTE, 1);

        return Task.FromResult<IReadOnlyList<DiagnosticFact>>(
        [
            new("diagnostics-runtime-fact-processors", DiagnosticFactValue.FromNumber(Environment.ProcessorCount)),
            new("diagnostics-runtime-fact-memory", DiagnosticFactValue.FromNumber(memoryMegabytes)),
            new("diagnostics-runtime-fact-collections", DiagnosticFactValue.FromNumber(GC.CollectionCount(0))),
        ]);
    }

    #endregion Public Methods
}
