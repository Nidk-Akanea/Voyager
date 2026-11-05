using Microsoft.Extensions.Options;

using Voyager.PluginHost;

namespace Voyager.GroupServer;

/// <summary>
/// Wires the plugin host into the group server, which runs the plugins (ADR-0018, decision 4; ADR-0020).
/// </summary>
internal static class PluginStartup
{
    #region Fields

    // Next to the server: /app/plugins in the image (ADR-0020, decision 3).
    private const string SHIPPED_FOLDER = "plugins";

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// Registers the plugin host with the shipped plugins only: installing plugins on the server comes with its own
    /// spec (ADR-0020), so there is no folder of installed plugins, no request and no state yet.
    /// </summary>
    /// <param name="services">The container of the server.</param>
    /// <param name="environment">The hosting environment: only Development may take the folder from a setting.</param>
    /// <returns>The same collection, to chain calls.</returns>
    public static IServiceCollection AddServerPlugins(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddPluginHost(provider =>
        {
            var configured = provider.GetRequiredService<IOptions<GroupServerOptions>>().Value.ShippedPluginsDirectory;
            var shipped = environment.IsDevelopment() && !string.IsNullOrWhiteSpace(configured)
                ? configured
                : Path.Combine(AppContext.BaseDirectory, SHIPPED_FOLDER);
            return new PluginLocations(shipped, UserDirectory: null);
        });
        services.AddSingleton<ServerDiagnosticFactSource>();
        services.AddSingleton<InterfaceFileCatalog>();
        return services;
    }

    /// <summary>
    /// Loads the plugins and adds the server's own contributions to the registry. Never throws because of a plugin:
    /// the catalog sets aside any plugin that fails, so that the server always starts.
    /// </summary>
    /// <param name="app">The web application, not started yet.</param>
    public static void LoadPlugins(this WebApplication app)
    {
        // Created now, so that the plugins are loaded once, at startup, and their failures are logged then.
        app.Services.GetRequiredService<PluginCatalog>();
        // The files of their interfaces too, so that the first call of the desktop app does not wait for their fingerprints.
        app.Services.GetRequiredService<InterfaceFileCatalog>();

        app.Services.GetRequiredService<ExtensionRegistry>()
            .AddDiagnosticFactSource(PluginCatalog.SERVICE_ID, app.Services.GetRequiredService<ServerDiagnosticFactSource>());
    }

    #endregion Public Methods
}
