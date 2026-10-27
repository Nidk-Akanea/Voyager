using NodaTime;

using Voyager.Sdk.Diagnostics;

namespace Voyager.Plugins.DiagnosticsClock;

/// <summary>
/// The current instant of the service, and for how many seconds it has been running. The instant goes out as an
/// instant: the interface shows it in the user's language and time zone.
/// </summary>
/// <param name="clock">Gives the current instant; a fake clock in tests.</param>
/// <param name="serviceStart">When the service started.</param>
public sealed class ClockFactSource(IClock clock, ServiceStart serviceStart) : IDiagnosticFactSource
{
    #region Public Methods

    /// <inheritdoc />
    public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetCurrentInstant();
        var uptimeSeconds = Math.Max(0, Math.Floor((now - serviceStart.StartedAt).TotalSeconds));

        return Task.FromResult<IReadOnlyList<DiagnosticFact>>(
        [
            new("diagnostics-clock-fact-now", DiagnosticFactValue.FromInstant(now)),
            new("diagnostics-clock-fact-uptime", DiagnosticFactValue.FromNumber(uptimeSeconds)),
        ]);
    }

    #endregion Public Methods
}
