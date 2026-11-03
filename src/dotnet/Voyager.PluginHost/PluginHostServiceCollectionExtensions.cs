using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NodaTime;

using Voyager.PluginHost.Packages;

namespace Voyager.PluginHost;

/// <summary>
/// Registers the plugin host in the container of a host (the local service, later the group server).
/// </summary>
public static class PluginHostServiceCollectionExtensions
{
    #region Public Methods

    /// <summary>
    /// Registers the store of the plugins, their catalog, the registry of extensions and the collector of diagnostic
    /// facts. The plugins are loaded when the catalog is first requested, which the host does at startup: the plugin a
    /// crash of the previous run designates is suspended, and the requests of the user are applied, before any plugin
    /// loads (ADR-0016, decisions 4 and 6). The host stops the <see cref="PluginActivity"/> when it stops normally.
    /// </summary>
    /// <param name="services">The container of the host.</param>
    /// <param name="locations">Finds where the plugins and the files that manage them are.</param>
    /// <returns>The same collection, to chain calls.</returns>
    public static IServiceCollection AddPluginHost(
        this IServiceCollection services,
        Func<IServiceProvider, PluginLocations> locations)
    {
        services.AddSingleton(provider => new PluginStore(
            locations(provider),
            ContractSupport.Application,
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<ILogger<PluginStore>>()));
        services.AddSingleton(provider => new PluginPackages(provider.GetRequiredService<PluginStore>().Locations, ContractSupport.Application));
        services.AddSingleton(provider => new PluginActivity(
            provider.GetRequiredService<PluginStore>().Locations.ActivityFile,
            provider.GetRequiredService<ILogger<PluginActivity>>()));
        services.AddSingleton(provider =>
        {
            var store = provider.GetRequiredService<PluginStore>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

            // The trace the previous run left designates the plugin it crashed with, if any (ADR-0016, decision 6).
            var suspect = PluginActivity.FindSuspect(store.Locations.ActivityFile, loggerFactory.CreateLogger<PluginActivity>());
            store.ApplyRequests(suspect);
            return PluginCatalog.Load(
                store.Locations.Roots,
                ContractSupport.Application,
                store.DisabledPlugins,
                loggerFactory,
                store.SuspendedPlugins,
                provider.GetRequiredService<PluginActivity>());
        });
        services.AddSingleton(provider => provider.GetRequiredService<PluginCatalog>().Registry);
        services.AddSingleton(PluginHostOptions.Default);
        services.AddSingleton<DiagnosticFactCollector>();
        return services;
    }

    #endregion Public Methods
}