using System.Security.Cryptography;
using System.Text.RegularExpressions;

using Voyager.PluginHost;

namespace Voyager.GroupServer;

/// <summary>
/// The files of the interface part of each plugin in use, that the desktop app downloads, checks and serves to its pages
/// (ADR-0020, decision 8): the module, what it imports under <c>ui</c>, and the translations. Read once, when the server
/// starts (<see cref="PluginStartup.LoadPlugins"/>): the plugins only change when it restarts (ADR-0020, decision 3).
/// </summary>
public sealed partial class InterfaceFileCatalog
{
    #region Fields

    // What the desktop app serves under ui: scripts only (src/ts/desktop/src/main/pluginFiles.ts).
    private static readonly string[] _scriptExtensions = [".js", ".mjs"];

    private static readonly string[] _languages = ["fr", "en"];

    // Links and junctions could lead out of the plugin's folder: never followed, never listed.
    private static readonly EnumerationOptions _scriptSearch = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System,
        IgnoreInaccessible = false,
    };

    private readonly ILogger<InterfaceFileCatalog> _logger;
    private readonly Dictionary<string, PublishedInterface> _published;

    /// <summary>
    /// The largest file the server publishes: an interface module is a few kilobytes. Below the 4 MiB of a gRPC message,
    /// with room for its envelope; the desktop app has the same limit (src/ts/desktop/src/main/interfaceModules.ts).
    /// </summary>
    public const long MAXIMUM_FILE_BYTES = 3 * 1024 * 1024;

    /// <summary>The most files a plugin's interface may have.</summary>
    public const int MAXIMUM_FILES = 200;

    #endregion Fields

    #region Public Constructors

    /// <summary>
    /// Creates the catalog of the interface files of the plugins the server loaded.
    /// </summary>
    /// <param name="catalog">The plugins found at startup.</param>
    /// <param name="locations">Where the plugins are.</param>
    /// <param name="logger">Says which plugin publishes no interface, and why.</param>
    public InterfaceFileCatalog(PluginCatalog catalog, PluginLocations locations, ILogger<InterfaceFileCatalog> logger)
    {
        _logger = logger;
        _published = ReadAll(catalog, locations);
    }

    #endregion Public Constructors

    #region Private Methods

    // A segment of a path the desktop app accepts: no hidden file, no parent folder, nothing a URL would need to escape.
    [GeneratedRegex("^[A-Za-z0-9_-][A-Za-z0-9_.-]*$")]
    private static partial Regex SafeSegment();

    private static bool IsSafe(string relativePath) =>
        relativePath.Split('/').All(segment => SafeSegment().IsMatch(segment));

    // A folder that is a link or a junction could lead out of the plugin's folder: it is never read.
    private static bool IsRealFolder(string folder) =>
        Directory.Exists(folder) && !new DirectoryInfo(folder).Attributes.HasFlag(FileAttributes.ReparsePoint);

    private static string Fingerprint(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    // The files of one plugin, or null when it publishes none: the reason is logged.
    private IReadOnlyList<InterfaceFileEntry>? ReadFiles(PluginState plugin, string folder)
    {
        if (!IsRealFolder(folder))
        {
            InterfaceFileLog.ModuleMissing(_logger, plugin.Id);
            return null;
        }

        var candidates = new List<string>();

        var uiFolder = Path.Combine(folder, "ui");
        if (IsRealFolder(uiFolder))
        {
            candidates.AddRange(Directory.EnumerateFiles(uiFolder, "*", _scriptSearch)
                .Where(file => _scriptExtensions.Contains(Path.GetExtension(file), StringComparer.Ordinal)));
        }

        var i18nFolder = Path.Combine(folder, "i18n");
        if (IsRealFolder(i18nFolder))
        {
            foreach (var language in _languages)
            {
                var languageFolder = Path.Combine(i18nFolder, language);
                var messages = Path.Combine(languageFolder, "messages.ftl");
                if (IsRealFolder(languageFolder)
                    && File.Exists(messages)
                    && !new FileInfo(messages).Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    candidates.Add(messages);
                }
            }
        }

        if (candidates.Count > MAXIMUM_FILES)
        {
            InterfaceFileLog.TooManyFiles(_logger, plugin.Id, candidates.Count, MAXIMUM_FILES);
            return null;
        }

        var files = new List<InterfaceFileEntry>();
        foreach (var file in candidates)
        {
            var relative = Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/');
            var size = new FileInfo(file).Length;
            if (!IsSafe(relative) || size > MAXIMUM_FILE_BYTES)
            {
                // The path comes from the disk: it is not logged, only that a file was left out.
                InterfaceFileLog.FileLeftOut(_logger, plugin.Id);
                continue;
            }

            files.Add(new InterfaceFileEntry(relative, Fingerprint(file), size));
        }

        if (!files.Any(file => file.Path == $"ui/{plugin.UiModule}"))
        {
            InterfaceFileLog.ModuleMissing(_logger, plugin.Id);
            return null;
        }

        return [.. files.OrderBy(file => file.Path, StringComparer.Ordinal)];
    }

    private Dictionary<string, PublishedInterface> ReadAll(PluginCatalog catalog, PluginLocations locations)
    {
        var published = new Dictionary<string, PublishedInterface>(StringComparer.Ordinal);
        foreach (var plugin in catalog.Plugins)
        {
            if (plugin.Status != PluginStatus.Loaded || plugin.UiModule is null || locations.RootOf(plugin.Origin) is not { } root)
            {
                continue;
            }

            var folder = Path.Combine(root, plugin.Folder);
            try
            {
                if (ReadFiles(plugin, folder) is { } files)
                {
                    published[plugin.Id] = new PublishedInterface(plugin.Version, folder, files);
                }
            }
            // One plugin that cannot be read costs its panel, never the server nor the other plugins.
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                InterfaceFileLog.Unreadable(_logger, plugin.Id, exception);
            }
        }

        return published;
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// The files of the interface of a plugin in use.
    /// </summary>
    /// <param name="pluginId">The identifier of the plugin.</param>
    /// <returns>Empty when the plugin is not in use, has no interface part, or could not publish it.</returns>
    public IReadOnlyList<InterfaceFileEntry> FilesOf(string pluginId) =>
        _published.TryGetValue(pluginId, out var published) ? published.Files : [];

    /// <summary>
    /// Reads a published file of the interface of a plugin in use.
    /// </summary>
    /// <param name="pluginId">The identifier of the plugin.</param>
    /// <param name="version">The version of the plugin the caller expects.</param>
    /// <param name="path">The path of the file, as <see cref="FilesOf"/> lists it.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <returns>The bytes of the file; null when no plugin in use has this identifier and version, or no such file.</returns>
    /// <exception cref="IOException">The file could not be read.</exception>
    public async Task<byte[]?> ReadAsync(string pluginId, string version, string path, CancellationToken cancellationToken)
    {
        if (!_published.TryGetValue(pluginId, out var published)
            || published.Version != version
            || !published.Files.Any(file => file.Path == path))
        {
            return null;
        }

        // The path is one the catalog listed, so it stays inside the plugin's folder.
        return await File.ReadAllBytesAsync(Path.Combine(published.Folder, path), cancellationToken);
    }

    #endregion Public Methods

    #region Private Types

    private sealed record PublishedInterface(string Version, string Folder, IReadOnlyList<InterfaceFileEntry> Files);

    #endregion Private Types
}
