using Microsoft.Extensions.Options;

using Voyager.PluginHost;

namespace Voyager.LocalService;

/// <summary>
/// Wires the plugin host into the local service.
/// </summary>
internal static class PluginStartup
{
    #region Private Methods

    // An empty setting is how a test or a host says "none".
    private static string? EmptyAsNull(string? folder) => string.IsNullOrWhiteSpace(folder) ? null : folder;

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Registers the plugin host, with the plugin folders given by the desktop app.
    /// </summary>
    /// <param name="services">The container of the service.</param>
    /// <returns>The same collection, to chain calls.</returns>
    public static IServiceCollection AddLocalPlugins(this IServiceCollection services)
    {
        services.AddPluginHost(provider =>
        {
            var options = provider.GetRequiredService<IOptions<LocalServiceOptions>>().Value;
            return new PluginLocations(EmptyAsNull(options.ShippedPluginsDirectory), EmptyAsNull(options.UserPluginsDirectory));
        });
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
        // Created now, so that the requests of the user are applied and the plugins loaded once, at startup, and their
        // failures are logged then.
        app.Services.GetRequiredService<PluginCatalog>();

        // A normal stop (the app closes, an update restarts it) is not a crash: the trace of the plugins being called
        // goes, so that none of them is suspected at the next start.
        var activity = app.Services.GetRequiredService<PluginActivity>();
        app.Lifetime.ApplicationStopping.Register(activity.Stop);

        app.Services.GetRequiredService<ExtensionRegistry>()
            .AddDiagnosticFactSource(PluginCatalog.SERVICE_ID, app.Services.GetRequiredService<ServiceDiagnosticFactSource>());
    }

    #endregion Public Methods
}