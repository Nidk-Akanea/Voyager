using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Voyager.PluginHost;

/// <summary>
/// The plugins found in the plugin folders at startup, and what they provide. Loading never throws: a
/// plugin that cannot be used is listed as rejected, with the reason, and the others load normally.
/// </summary>
public sealed class PluginCatalog : IDisposable, IAsyncDisposable
{
    #region Fields

    // What the log says of a folder whose name is not a plugin identifier: it is free text.
    private const string INVALID_FOLDER_NAME = "(invalid folder name)";

    private const string MANIFEST_FILE_NAME = "plugin.json";

    private const long MAXIMUM_MANIFEST_BYTES = 64 * 1024;

    // Folders the host itself creates next to the plugins (a plugin being removed): never a plugin.
    private const char INTERNAL_FOLDER_PREFIX = '.';

    private readonly List<(string PluginId, ServiceProvider Container)> _containers = [];

    private readonly ILogger _logger;

    /// <summary>
    /// The identifier under which the service itself provides extensions. No plugin may use it, so that
    /// a plugin can never pass for the service.
    /// </summary>
    public const string SERVICE_ID = "service";

    #endregion Fields

    #region Private Constructors

    private PluginCatalog(IReadOnlyList<PluginState> plugins, ExtensionRegistry registry, ILogger logger)
    {
        Plugins = plugins;
        Registry = registry;
        _logger = logger;
    }

    #endregion Private Constructors

    #region Properties

    /// <summary>
    /// Every plugin folder found, loaded, rejected or disabled: the folders of each root in the order of the roots,
    /// and in alphabetical order of folder names within a root.
    /// </summary>
    public IReadOnlyList<PluginState> Plugins { get; }

    /// <summary>What the loaded plugins provide; the host adds the service's own contributions to it.</summary>
    public ExtensionRegistry Registry { get; }

    #endregion Properties

    #region Private Methods

    private static string[] FindPluginDirectories(string pluginsFolder, ILogger logger)
    {
        try
        {
            return Directory.Exists(pluginsFolder)
                ?
                [
                    .. Directory.EnumerateDirectories(pluginsFolder)
                        .Where(directory => !Path.GetFileName(directory).StartsWith(INTERNAL_FOLDER_PREFIX))
                        .OrderBy(Path.GetFileName, StringComparer.Ordinal),
                ]
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            PluginHostLog.PluginFolderUnreadable(logger, exception);
            return [];
        }
    }

    private static string LoggableName(string folder) =>
        PluginManifestReader.IsValidIdentifier(folder) ? folder : INVALID_FOLDER_NAME;

    // A plugin whose manifest could not be read: only its folder is known.
    private static PluginState Unreadable(string folder, PluginOrigin origin, PluginRejectionReason reason) =>
        new(folder, origin, folder, string.Empty, PluginStatus.Rejected, reason, UiModule: null, Provides: [], Contracts: [], HasDotnet: false, HasUi: false);

    // A plugin whose manifest was read, and that is not in use: what it is and what it would provide is known.
    private static PluginState NotInUse(
        string folder,
        PluginOrigin origin,
        PluginManifest manifest,
        IReadOnlyList<ContractUse> contracts,
        PluginStatus status,
        PluginRejectionReason? reason = null) =>
        new(
            folder,
            origin,
            manifest.Id,
            manifest.Version,
            status,
            reason,
            UiModule: null,
            Provides: [],
            contracts,
            HasDotnet: manifest.DotnetAssembly is not null,
            HasUi: manifest.UiModule is not null);

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
    private PluginState ReadAndLoad(string directory, PluginOrigin origin, LoadContext context)
    {
        var folder = Path.GetFileName(directory);
        var manifestPath = Path.Combine(directory, MANIFEST_FILE_NAME);
        if (!TryReadManifest(manifestPath, out var manifest))
        {
            PluginHostLog.PluginRejected(_logger, LoggableName(folder), PluginRejectionReason.ManifestInvalid);
            return Unreadable(folder, origin, PluginRejectionReason.ManifestInvalid);
        }

        // Known as soon as the manifest is read, so that every state from here on shows them.
        var uses = context.Contracts.Describe(manifest);

        // The folder name is what the interface asks for: it must be the identifier, and never the service's.
        // Checked before the identifier is claimed, so that a stray copy of a plugin under another folder
        // name cannot make the real plugin look like the duplicate: a folder under another name is only a
        // mismatch while the identifier is still unclaimed, otherwise it is the duplicate.
        if (string.Equals(manifest.Id, SERVICE_ID, StringComparison.Ordinal))
        {
            PluginHostLog.PluginIdReserved(_logger, manifest.Id);
            return NotInUse(folder, origin, manifest, uses, PluginStatus.Rejected, PluginRejectionReason.ManifestInvalid);
        }

        var folderIsId = string.Equals(folder, manifest.Id, StringComparison.Ordinal);
        if (!folderIsId && !context.SeenIds.ContainsKey(manifest.Id))
        {
            PluginHostLog.PluginFolderNameMismatch(_logger, manifest.Id);
            return NotInUse(folder, origin, manifest, uses, PluginStatus.Rejected, PluginRejectionReason.ManifestInvalid);
        }

        // A disabled plugin claims nothing: an installed plugin with the identifier of a disabled shipped one takes its place.
        if (folderIsId && context.Disabled.Contains(new PluginKey(origin, manifest.Id)))
        {
            PluginHostLog.PluginDisabled(_logger, manifest.Id, origin);
            return NotInUse(folder, origin, manifest, uses, PluginStatus.Disabled);
        }

        if (context.SeenIds.TryGetValue(manifest.Id, out var claimedBy))
        {
            var duplicate = claimedBy == PluginOrigin.Shipped && origin == PluginOrigin.Installed
                ? PluginRejectionReason.DuplicateOfShipped
                : PluginRejectionReason.DuplicateId;
            PluginHostLog.PluginRejected(_logger, manifest.Id, duplicate);
            return NotInUse(folder, origin, manifest, uses, PluginStatus.Rejected, duplicate);
        }

        context.SeenIds.Add(manifest.Id, origin);

        // Every contract is checked, so that the user sees all of those that block the plugin, not the first one.
        var unsupported = uses.Where(use => !ContractSupport.IsAccepted(use.State)).ToList();
        foreach (var use in unsupported)
        {
            PluginHostLog.ContractNotSupported(_logger, manifest.Id, use.Contract, use.Version, use.State);
        }

        if (unsupported.Count > 0)
        {
            return NotInUse(folder, origin, manifest, uses, PluginStatus.Rejected, PluginRejectionReason.ContractNotSupported);
        }

        foreach (var use in uses.Where(use => use.State == ContractUseState.Deprecated))
        {
            PluginHostLog.ContractDeprecated(_logger, manifest.Id, use.Contract, use.Version);
        }

        var result = context.Loader.Load(directory, manifest);
        if (result.Rejection is { } reason)
        {
            return NotInUse(folder, origin, manifest, uses, PluginStatus.Rejected, reason);
        }

        // Registered only now that nothing can fail any more: a rejected plugin leaves nothing behind.
        foreach (var source in result.Sources)
        {
            context.Registry.AddDiagnosticFactSource(manifest.Id, source);
        }

        if (result.Provider is not null)
        {
            _containers.Add((manifest.Id, result.Provider));
        }

        PluginHostLog.PluginLoaded(_logger, manifest.Id, manifest.Version, origin);
        return new PluginState(
            folder,
            origin,
            manifest.Id,
            manifest.Version,
            PluginStatus.Loaded,
            RejectionReason: null,
            manifest.UiModule,
            [.. manifest.Provides.Select(reference => reference.Contract)],
            uses,
            HasDotnet: manifest.DotnetAssembly is not null,
            HasUi: manifest.UiModule is not null);
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Finds and loads the plugins of a single folder of shipped plugins, none of them disabled.
    /// </summary>
    /// <param name="pluginsFolder">The folder that holds one sub-folder per plugin; null or missing means no plugin.</param>
    /// <param name="contracts">The versions of contracts the application accepts, to check each plugin against.</param>
    /// <param name="loggerFactory">Where the host and the plugins write their logs.</param>
    /// <returns>The catalog; it owns the plugins' containers and must be disposed with the host.</returns>
    internal static PluginCatalog Load(string? pluginsFolder, ContractSupport contracts, ILoggerFactory loggerFactory) =>
        Load(
            string.IsNullOrWhiteSpace(pluginsFolder) ? [] : [new PluginRoot(pluginsFolder, PluginOrigin.Shipped)],
            contracts,
            new HashSet<PluginKey>(),
            loggerFactory);

    /// <summary>
    /// Finds and loads the plugins of several folders, in their order: an identifier is used once, by the first
    /// plugin that claims it (ADR-0016, decision 1), and a disabled plugin is listed without being loaded.
    /// </summary>
    /// <param name="roots">The folders of plugins, shipped first, then installed; none means no plugin.</param>
    /// <param name="contracts">The versions of contracts the application accepts, to check each plugin against.</param>
    /// <param name="disabled">The plugins the user disabled.</param>
    /// <param name="loggerFactory">Where the host and the plugins write their logs.</param>
    /// <returns>The catalog; it owns the plugins' containers and must be disposed with the host.</returns>
    public static PluginCatalog Load(
        IReadOnlyList<PluginRoot> roots,
        ContractSupport contracts,
        IReadOnlySet<PluginKey> disabled,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<PluginCatalog>();
        var registry = new ExtensionRegistry();
        if (roots.Count == 0)
        {
            PluginHostLog.NoPluginFolder(logger);
            return new PluginCatalog([], registry, logger);
        }

        var context = new LoadContext(contracts, disabled, new Dictionary<string, PluginOrigin>(StringComparer.Ordinal), new PluginLoader(loggerFactory), registry);
        var plugins = new List<PluginState>();

        // The catalog exists before the plugins load, so that it owns their containers as they appear.
        var catalog = new PluginCatalog(plugins, registry, logger);
        foreach (var root in roots)
        {
            foreach (var directory in FindPluginDirectories(root.Path, logger))
            {
                try
                {
                    plugins.Add(catalog.ReadAndLoad(directory, root.Origin, context));
                }
                // A plugin folder is a boundary: nothing it holds can keep the others from loading.
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    PluginHostLog.PluginFolderSkipped(logger, exception);
                    plugins.Add(Unreadable(Path.GetFileName(directory), root.Origin, PluginRejectionReason.LoadFailed));
                }
            }
        }

        var loadedCount = plugins.Count(plugin => plugin.Status == PluginStatus.Loaded);
        var disabledCount = plugins.Count(plugin => plugin.Status == PluginStatus.Disabled);
        PluginHostLog.PluginsLoaded(logger, loadedCount, plugins.Count - loadedCount - disabledCount, disabledCount);
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

    #region Nested Types

    // What the loading of every folder shares: the rule for contracts, the choices of the user, and what is claimed.
    private sealed record LoadContext(
        ContractSupport Contracts,
        IReadOnlySet<PluginKey> Disabled,
        Dictionary<string, PluginOrigin> SeenIds,
        PluginLoader Loader,
        ExtensionRegistry Registry);

    #endregion Nested Types
}
