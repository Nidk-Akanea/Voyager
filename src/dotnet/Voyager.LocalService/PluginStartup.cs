using Microsoft.Extensions.Options;

using Voyager.PluginHost;

namespace Voyager.LocalService;

/// <summary>
/// Wires the plugin host into the local service.
/// </summary>
internal static class PluginStartup
{
    #region Public Methods

    /// <summary>
    /// Registers the plugin host, with the plugin folder given by the desktop app.
    /// </summary>
    /// <param name="services">The container of the service.</param>
    /// <returns>The same collection, to chain calls.</returns>
    public static IServiceCollection AddLocalPlugins(this IServiceCollection services)
    {
        services.AddPluginHost(provider => provider.GetRequiredService<IOptions<LocalServiceOptions>>().Value.PluginsDirectory);
        services.AddSingleton<ServiceDiagnosticFactSource>();
        return services;
    }

    /// <summary>
    /// Loads the plugins and adds the service's own contributions to the registry. Never throws because of
    /// a plugin: the catalog sets aside any plugin that fails, so that the service always starts.
    /// </summary>
    /// <param name="app">The web application, not started yet.</param>
    public static void LoadPlugins(this WebApplication app)
    {
        // Created now, so that the plugins are loaded once, at startup, and their failures are logged then.
        app.Services.GetRequiredService<PluginCatalog>();

        app.Services.GetRequiredService<ExtensionRegistry>()
            .AddDiagnosticFactSource(PluginCatalog.SERVICE_ID, app.Services.GetRequiredService<ServiceDiagnosticFactSource>());
    }

    #endregion Public Methods
}
