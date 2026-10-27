using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Voyager.Sdk;

namespace Voyager.PluginHost;

/// <summary>
/// Registers the plugin host in the container of a host (the local service, later the group server).
/// </summary>
public static class PluginHostServiceCollectionExtensions
{
    #region Public Methods

    /// <summary>
    /// Registers the catalog of plugins, the registry of extensions and the collector of diagnostic
    /// facts. The plugins are loaded when the catalog is first requested, which the host does at startup.
    /// </summary>
    /// <param name="services">The container of the host.</param>
    /// <param name="pluginsFolder">Finds the folder that holds the plugins; it may return null for none.</param>
    /// <returns>The same collection, to chain calls.</returns>
    public static IServiceCollection AddPluginHost(
        this IServiceCollection services,
        Func<IServiceProvider, string?> pluginsFolder)
    {
        services.AddSingleton(provider => PluginCatalog.Load(
            pluginsFolder(provider),
            SdkInfo.Version,
            provider.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton(provider => provider.GetRequiredService<PluginCatalog>().Registry);
        services.AddSingleton(PluginHostOptions.Default);
        services.AddSingleton<DiagnosticFactCollector>();
        return services;
    }

    #endregion Public Methods
}
