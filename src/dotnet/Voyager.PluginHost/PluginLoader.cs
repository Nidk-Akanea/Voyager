using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Voyager.Sdk;
using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginHost;

/// <summary>
/// Loads the .NET part of one plugin: its own load context, its own container, and a check of what it
/// registered against its manifest. Every failure becomes a rejection; nothing leaks to the caller.
/// </summary>
internal sealed class PluginLoader(ILoggerFactory loggerFactory)
{
    #region Fields

    private readonly ILogger _logger = loggerFactory.CreateLogger<PluginLoader>();

    #endregion Fields

    #region Private Methods

    private static Type? FindPluginType(Assembly assembly)
    {
        var candidates = assembly.GetExportedTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IPlugin).IsAssignableFrom(type))
            .ToList();

        return candidates.Count == 1 ? candidates[0] : null;
    }

    // A plugin may only provide what its manifest announces: the manifest is what the host and the user can read.
    private static ExtensionRegistration? FindUndeclaredRegistration(PluginBuilder builder, PluginManifest manifest) =>
        builder.Registrations.FirstOrDefault(registration => !manifest.Provides.Contains(registration.ExtensionPoint));

    private ServiceCollection CreatePrivateServices()
    {
        // A container of the plugin's own: the host's services are not in it, so a plugin can neither
        // replace one nor reach one. It only gets logging, wired to the host's log outputs.
        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        return services;
    }

    // The sources a plugin registered are created here, so that a constructor that throws rejects the
    // plugin now instead of failing the first call.
    private static List<IDiagnosticFactSource> ResolveSources(ServiceProvider provider, PluginBuilder builder) =>
    [
        .. builder.Registrations
            .Where(registration => registration.ExtensionPoint == ExtensionPoints.DIAGNOSTIC_FACT_SOURCE)
            .Select(registration => (IDiagnosticFactSource)provider.GetRequiredService(registration.ImplementationType)),
    ];

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Loads the .NET part of a plugin, if it has one.
    /// </summary>
    /// <param name="folder">The folder of the plugin.</param>
    /// <param name="manifest">Its validated manifest.</param>
    /// <returns>What the plugin provides, or the reason it was rejected.</returns>
    public PluginLoadResult Load(string folder, PluginManifest manifest)
    {
        if (manifest.DotnetAssembly is null)
        {
            return PluginLoadResult.Success(provider: null, sources: []);
        }

        ServiceProvider? provider = null;
        try
        {
            var context = new PluginLoadContext(manifest.Id, folder);
            var assembly = context.LoadFromAssemblyPath(Path.Combine(folder, manifest.DotnetAssembly));
            var pluginType = FindPluginType(assembly);
            if (pluginType is null)
            {
                PluginHostLog.PluginTypeNotFound(_logger, manifest.Id);
                return PluginLoadResult.Failure(PluginRejectionReason.LoadFailed);
            }

            var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
            var services = CreatePrivateServices();
            var builder = new PluginBuilder(manifest.Id, services);
            plugin.Configure(builder);

            if (FindUndeclaredRegistration(builder, manifest) is { } undeclared)
            {
                PluginHostLog.UndeclaredRegistration(_logger, manifest.Id, undeclared.ExtensionPoint);
                return PluginLoadResult.Failure(PluginRejectionReason.UndeclaredRegistration);
            }

            // Same validation as the host's own container: a missing dependency fails now, not at the first call.
            provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            return PluginLoadResult.Success(provider, ResolveSources(provider, builder));
        }
        // A plugin is a boundary: whatever it throws while loading rejects this plugin and no other.
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            provider?.Dispose();
            PluginHostLog.PluginLoadFailed(_logger, manifest.Id, exception);
            return PluginLoadResult.Failure(PluginRejectionReason.LoadFailed);
        }
    }

    #endregion Public Methods
}
