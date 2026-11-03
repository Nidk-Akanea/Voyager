using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Voyager.PluginHost;

/// <summary>
/// Registers the plugin host in the container of a host (the local service, later the group server).
/// </summary>
public static class PluginHostServiceCollectionExtensions
{
    #region Public Methods

    /// <summary>
    /// Registers the store of the plugins, their catalog, the registry of extensions and the collector of diagnostic
    /// facts. The plugins are loaded when the catalog is first requested, which the host does at startup: the
    /// requests of the user are applied first, before any plugin loads (ADR-0016, decision 4).
    /// </summary>
    /// <param name="services">The container of the host.</param>
    /// <param name="locations">Finds where the plugins and the files that manage them are.</param>
    /// <returns>The same collection, to chain calls.</returns>
    public static IServiceCollection AddPluginHost(
        this IServiceCollection services,
        Func<IServiceProvider, PluginLocations> locations)
    {
        services.AddSingleton(provider => new PluginStore(locations(provider), provider.GetRequiredService<ILogger<PluginStore>>()));
        services.AddSingleton(provider =>
        {
            var store = provider.GetRequiredService<PluginStore>();
            store.ApplyRequests();
            return PluginCatalog.Load(
                store.Locations.Roots,
                ContractSupport.Application,
                store.DisabledPlugins,
                provider.GetRequiredService<ILoggerFactory>());
        });
        services.AddSingleton(provider => provider.GetRequiredService<PluginCatalog>().Registry);
        services.AddSingleton(PluginHostOptions.Default);
        services.AddSingleton<DiagnosticFactCollector>();
        return services;
    }

    #endregion Public Methods
}
