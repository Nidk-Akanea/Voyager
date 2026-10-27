using Voyager.Sdk;

namespace Voyager.Plugins.DiagnosticsRuntime;

/// <summary>
/// Demonstration plugin of the plugin proof (ADR-0015): facts about the runtime the service runs on, shown
/// by the standard list of the "About" page. It has no interface part.
/// </summary>
public sealed class DiagnosticsRuntimePlugin : IPlugin
{
    #region Public Methods

    /// <inheritdoc />
    public void Configure(IPluginBuilder builder) => builder.AddDiagnosticFactSource<RuntimeFactSource>();

    #endregion Public Methods
}
