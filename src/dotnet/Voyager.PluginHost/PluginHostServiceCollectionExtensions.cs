using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Voyager.PluginHost;

/// <summary>
/// Registers the plugin host in the container of the group server, which runs the plugins (ADR-0018, ADR-0020).
/// </summary>
public static class PluginHostServiceCollectionExtensions
{
    #region Public Methods

    /// <summary>
    /// Registers where the plugins are, their catalog, the registry of extensions and the collector of diagnostic facts.
    /// The plugins are loaded when the catalog is first requested, which the host does at startup. Disabling or suspending
    /// a plugin comes with the management of plugins on the server (ADR-0020): every plugin found is loaded for now.
    /// </summary>
    /// <param name="services">The container of the host.</param>
    /// <param name="locations">Finds where the plugins are.</param>
    /// <returns>The same collection, to chain calls.</returns>
    public static IServiceCollection AddPluginHost(
        this IServiceCollection services,
        Func<IServiceProvider, PluginLocations> locations)
    {
        services.AddSingleton(locations);
        services.AddSingleton(provider => PluginCatalog.Load(
            provider.GetRequiredService<PluginLocations>().Roots,
            ContractSupport.Application,
            new HashSet<PluginKey>(),
            provider.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton(provider => provider.GetRequiredService<PluginCatalog>().Registry);
        services.AddSingleton(PluginHostOptions.Default);
        services.AddSingleton<DiagnosticFactCollector>();
        return services;
    }

    #endregion Public Methods
}
