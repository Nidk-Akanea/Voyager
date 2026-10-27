using Microsoft.Extensions.DependencyInjection;

using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginHost;

/// <summary>
/// The outcome of loading the .NET part of one plugin.
/// </summary>
internal sealed record PluginLoadResult
{
    #region Constructors

    private PluginLoadResult(
        PluginRejectionReason? rejection,
        ServiceProvider? provider,
        IReadOnlyList<IDiagnosticFactSource> sources)
    {
        Rejection = rejection;
        Provider = provider;
        Sources = sources;
    }

    #endregion Constructors

    #region Properties

    /// <summary>Why the plugin was rejected; null when it loaded.</summary>
    public PluginRejectionReason? Rejection { get; }

    /// <summary>The plugin's private container, to dispose with the host; null when the plugin has no .NET part.</summary>
    public ServiceProvider? Provider { get; }

    /// <summary>The sources of diagnostic facts the plugin provides.</summary>
    public IReadOnlyList<IDiagnosticFactSource> Sources { get; }

    #endregion Properties

    #region Public Methods

    /// <summary>The plugin loaded.</summary>
    public static PluginLoadResult Success(ServiceProvider? provider, IReadOnlyList<IDiagnosticFactSource> sources) =>
        new(null, provider, sources);

    /// <summary>The plugin was rejected for <paramref name="reason"/>.</summary>
    public static PluginLoadResult Failure(PluginRejectionReason reason) => new(reason, null, []);

    #endregion Public Methods
}
