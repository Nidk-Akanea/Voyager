using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using Voyager.Sdk;

namespace Voyager.Plugins.DiagnosticsClock;

/// <summary>
/// Demonstration plugin of the plugin proof (ADR-0015): the time of the service and how long it has been
/// running. It brings its own panel for the "About" page, with a button that asks for the facts again.
/// </summary>
public sealed class DiagnosticsClockPlugin : IPlugin
{
    #region Public Methods

    /// <inheritdoc />
    public void Configure(IPluginBuilder builder)
    {
        // The services of a plugin are its own: it brings its clock, as the host's is not shared with it.
        builder.Services.AddSingleton<IClock>(SystemClock.Instance);
        builder.Services.AddSingleton<ServiceStart>();
        builder.AddDiagnosticFactSource<ClockFactSource>();
    }

    #endregion Public Methods
}
