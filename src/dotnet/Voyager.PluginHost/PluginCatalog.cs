using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Voyager.PluginHost;

/// <summary>
/// The plugins found in the plugin folder at startup, and what they provide. Loading never throws: a
/// plugin that cannot be used is listed as rejected, with the reason, and the others load normally.
/// </summary>
public sealed class PluginCatalog : IDisposable, IAsyncDisposable
{
    #region Fields

    /// <summary>
    /// The identifier under which the service itself provides extensions. No plugin may use it, so that
    /// a plugin can never pass for the service.
    /// </summary>
    public const string SERVICE_ID = "service";

    private const string MANIFEST_FILE_NAME = "plugin.json";
    private const long MAXIMUM_MANIFEST_BYTES = 64 * 1024;

    // What the log says of a folder whose name is not a plugin identifier: it is free text.
    private const string INVALID_FOLDER_NAME = "(invalid folder name)";

    private readonly List<(string PluginId, ServiceProvider Container)> _containers = [];
    private readonly ILogger _logger;

    #endregion Fields

    #region Constructors

    private PluginCatalog(IReadOnlyList<PluginState> plugins, ExtensionRegistry registry, ILogger logger)
    {
        Plugins = plugins;
        Registry = registry;
        _logger = logger;
    }

    #endregion Constructors

    #region Properties

    /// <summary>Every plugin folder found, loaded or rejected, in alphabetical order of folder names.</summary>
    public IReadOnlyList<PluginState> Plugins { get; }

    /// <summary>What the loaded plugins provide; the host adds the service's own contributions to it.</summary>
    public ExtensionRegistry Registry { get; }

    #endregion Properties

    #region Private Methods

    private static PluginState Rejected(string folder, string id, string version, PluginRejectionReason reason) =>
        new(folder, id, version, PluginStatus.Rejected, reason, UiModule: null, Provides: []);

    private static string LoggableName(string folder) =>
        PluginManifestReader.IsValidIdentifier(folder) ? folder : INVALID_FOLDER_NAME;

    private static bool TryReadManifest(string manifestPath, out PluginManifest manifest)
    {
        manifest = null!;
        try
        {
            var file = new FileInfo(manifestPath);
            if (!file.Exists || file.Length > MAXIMUM_MANIFEST_BYTES)
            {
                return false;
            }

            return PluginManifestReader.TryRead(File.ReadAllText(manifestPath), out manifest!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // An unreadable file is an invalid manifest: the plugin is set aside like any other.
            return false;
        }
    }

    // Everything that decides the fate of one plugin folder. Each step that rejects a plugin says why.
    private PluginState ReadAndLoad(
        string directory,
        Version sdkVersion,
        HashSet<string> seenIds,
        PluginLoader loader,
        ExtensionRegistry registry)
    {
        var folder = Path.GetFileName(directory);
        var manifestPath = Path.Combine(directory, MANIFEST_FILE_NAME);
        if (!TryReadManifest(manifestPath, out var manifest))
        {
            PluginHostLog.PluginRejected(_logger, LoggableName(folder), PluginRejectionReason.ManifestInvalid);
            return Rejected(folder, folder, string.Empty, PluginRejectionReason.ManifestInvalid);
        }

        // The folder name is what the interface asks for: it must be the identifier, and never the service's.
        // Checked before the identifier is claimed, so that a stray copy of a plugin under another folder
        // name cannot make the real plugin look like the duplicate: a folder under another name is only a
        // mismatch while the identifier is still unclaimed, otherwise it is the duplicate.
        if (string.Equals(manifest.Id, SERVICE_ID, StringComparison.Ordinal))
        {
            PluginHostLog.PluginIdReserved(_logger, manifest.Id);
            return Rejected(folder, manifest.Id, manifest.Version, PluginRejectionReason.ManifestInvalid);
        }

        var folderIsId = string.Equals(folder, manifest.Id, StringComparison.Ordinal);
        if (!folderIsId && !seenIds.Contains(manifest.Id))
        {
            PluginHostLog.PluginFolderNameMismatch(_logger, manifest.Id);
            return Rejected(folder, manifest.Id, manifest.Version, PluginRejectionReason.ManifestInvalid);
        }

        if (!seenIds.Add(manifest.Id))
        {
            PluginHostLog.PluginRejected(_logger, manifest.Id, PluginRejectionReason.DuplicateId);
            return Rejected(folder, manifest.Id, manifest.Version, PluginRejectionReason.DuplicateId);
        }

        if (!SdkCompatibility.IsAccepted(manifest.Sdk, sdkVersion))
        {
            PluginHostLog.SdkIncompatible(_logger, manifest.Id, manifest.Sdk, sdkVersion.ToString(2));
            return Rejected(folder, manifest.Id, manifest.Version, PluginRejectionReason.SdkIncompatible);
        }

        var result = loader.Load(directory, manifest);
        if (result.Rejection is { } reason)
        {
            return Rejected(folder, manifest.Id, manifest.Version, reason);
        }

        // Registered only now that nothing can fail any more: a rejected plugin leaves nothing behind.
        foreach (var source in result.Sources)
        {
            registry.AddDiagnosticFactSource(manifest.Id, source);
        }

        if (result.Provider is not null)
        {
            _containers.Add((manifest.Id, result.Provider));
        }

        PluginHostLog.PluginLoaded(_logger, manifest.Id, manifest.Version);
        return new PluginState(
            folder,
            manifest.Id,
            manifest.Version,
            PluginStatus.Loaded,
            RejectionReason: null,
            manifest.UiModule,
            manifest.Provides);
    }

    private static string[] FindPluginDirectories(string pluginsFolder, ILogger logger)
    {
        try
        {
            return Directory.Exists(pluginsFolder)
                ? [.. Directory.EnumerateDirectories(pluginsFolder).OrderBy(Path.GetFileName, StringComparer.Ordinal)]
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            PluginHostLog.PluginFolderUnreadable(logger, exception);
            return [];
        }
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Finds and loads the plugins of a folder.
    /// </summary>
    /// <param name="pluginsFolder">The folder that holds one sub-folder per plugin; null or missing means no plugin.</param>
    /// <param name="sdkVersion">The version of the SDK that runs, to check each plugin against.</param>
    /// <param name="loggerFactory">Where the host and the plugins write their logs.</param>
    /// <returns>The catalog; it owns the plugins' containers and must be disposed with the host.</returns>
    public static PluginCatalog Load(string? pluginsFolder, Version sdkVersion, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<PluginCatalog>();
        var registry = new ExtensionRegistry();
        if (string.IsNullOrWhiteSpace(pluginsFolder))
        {
            PluginHostLog.NoPluginFolder(logger);
            return new PluginCatalog([], registry, logger);
        }

        var directories = FindPluginDirectories(pluginsFolder, logger);
        var loader = new PluginLoader(loggerFactory);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var plugins = new List<PluginState>();

        // The catalog exists before the plugins load, so that it owns their containers as they appear.
        var catalog = new PluginCatalog(plugins, registry, logger);
        foreach (var directory in directories)
        {
            try
            {
                plugins.Add(catalog.ReadAndLoad(directory, sdkVersion, seenIds, loader, registry));
            }
            // A plugin folder is a boundary: nothing it holds can keep the others from loading.
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                PluginHostLog.PluginFolderSkipped(logger, exception);
                var folder = Path.GetFileName(directory);
                plugins.Add(Rejected(folder, folder, string.Empty, PluginRejectionReason.LoadFailed));
            }
        }

        var loadedCount = plugins.Count(plugin => plugin.Status == PluginStatus.Loaded);
        PluginHostLog.PluginsLoaded(logger, loadedCount, plugins.Count - loadedCount);
        return catalog;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (pluginId, container) in _containers)
        {
            try
            {
                container.Dispose();
            }
            // One plugin that fails to shut down must not keep the others from shutting down.
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                PluginHostLog.PluginContainerDisposeFailed(_logger, pluginId, exception);
            }
        }

        _containers.Clear();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var (pluginId, container) in _containers)
        {
            try
            {
                await container.DisposeAsync();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                PluginHostLog.PluginContainerDisposeFailed(_logger, pluginId, exception);
            }
        }

        _containers.Clear();
    }

    #endregion Public Methods
}
