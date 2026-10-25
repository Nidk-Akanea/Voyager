using System.Reflection;

using NodaTime;

namespace Voyager.LocalService;

/// <summary>
/// Facts about this process reported by the diagnostics: its version and when it started.
/// Registered as a singleton and created at startup, so <see cref="StartedAt"/> is the real start.
/// </summary>
/// <param name="clock">Gives the start time; a fake clock in tests.</param>
public sealed class ServiceInfo(IClock clock)
{
    #region Fields

    private const string UNKNOWN_VERSION = "unknown";

    #endregion Fields

    #region Properties

    /// <summary>When the service started.</summary>
    public Instant StartedAt { get; } = clock.GetCurrentInstant();

    /// <summary>
    /// The version computed by GitVersion (ADR-0007), without its build metadata (branch, commit).
    /// </summary>
    public string Version { get; } = ReadVersion();

    #endregion Properties

    #region Private Methods

    private static string ReadVersion()
    {
        var informational = typeof(ServiceInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrEmpty(informational))
        {
            return UNKNOWN_VERSION;
        }

        var metadataStart = informational.IndexOf('+', StringComparison.Ordinal);
        return metadataStart < 0 ? informational : informational[..metadataStart];
    }

    #endregion Private Methods
}
