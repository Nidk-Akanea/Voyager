using System.Diagnostics;

using NodaTime;

namespace Voyager.Plugins.DiagnosticsClock;

/// <summary>
/// When the process of the service started, as the operating system knows it. Not sealed on purpose: the tests
/// replace it with a fixed start. (The operating system gives a local time: during the repeated hour of a change
/// to winter time the instant can be off by an hour, which only this demonstration shows.)
/// </summary>
public class ServiceStart
{
    #region Properties

    /// <summary>The instant the process started.</summary>
    public virtual Instant StartedAt { get; } = ReadProcessStart();

    #endregion Properties

    #region Private Methods

    private static Instant ReadProcessStart()
    {
        using var process = Process.GetCurrentProcess();
        return Instant.FromDateTimeUtc(process.StartTime.ToUniversalTime());
    }

    #endregion Private Methods
}
