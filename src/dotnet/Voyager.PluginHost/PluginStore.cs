using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace Voyager.PluginHost;

/// <summary>
/// The management of the plugins between two starts (ADR-0016, decision 4): it reads the requests the desktop app
/// wrote after the user confirmed them, applies them before any plugin loads, and writes the state. The desktop app
/// is the only one to write the requests and the service the only one to write the state.
/// </summary>
/// <param name="locations">Where the plugins and the files that manage them are.</param>
/// <param name="logger">Where the store writes its logs: identifiers of plugins and requests, never a path.</param>
public sealed class PluginStore(PluginLocations locations, ILogger<PluginStore> logger)
{
    #region Fields

    // What a plugin being removed is renamed to, so that a removal that stops half way never leaves half a plugin
    // under its own name: the catalog skips such folders, and the next start deletes them.
    private const string REMOVAL_PREFIX = ".removing-";

    private Dictionary<PluginKey, PluginRecord> _records = [];

    private HashSet<string> _appliedRequests = new(StringComparer.Ordinal);

    #endregion Fields

    #region Properties

    /// <summary>Where the plugins and the files that manage them are.</summary>
    public PluginLocations Locations { get; } = locations;

    /// <summary>What the service remembers of each plugin, once the requests are applied.</summary>
    public IReadOnlyCollection<PluginRecord> Records => _records.Values;

    /// <summary>The plugins the user disabled, once the requests are applied.</summary>
    public IReadOnlySet<PluginKey> DisabledPlugins =>
        _records.Values.Where(record => !record.Enabled).Select(record => record.Plugin).ToHashSet();

    #endregion Properties

    #region Private Methods

    private static PluginRecord DefaultRecord(PluginKey plugin) =>
        new(plugin, Enabled: true, InstalledAt: null, PackageName: null, PackageSha256: null, LastError: null);

    // Null when the file exists but cannot be read now: nothing is known of the requests then, which is not the same
    // as having none (an absent file).
    private IReadOnlyList<PluginRequest>? ReadRequests()
    {
        if (Locations.RequestsFile is not { } path)
        {
            return [];
        }

        try
        {
            var requests = PluginStoreFiles.ReadRequests(path, out var ignored);
            if (ignored > 0)
            {
                PluginHostLog.RequestsIgnored(logger, ignored);
            }

            return requests;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            // Requests that cannot be read change nothing: the service starts with the plugins as they were.
            PluginHostLog.RequestsUnreadable(logger, exception);
            return null;
        }
    }

    private void ReadState()
    {
        if (Locations.StateFile is not { } path)
        {
            return;
        }

        try
        {
            var (records, appliedRequests) = PluginStoreFiles.ReadState(path);
            _records = records.GroupBy(record => record.Plugin).ToDictionary(group => group.Key, group => group.Last());
            _appliedRequests = new HashSet<string>(appliedRequests, StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            // A lost state enables every plugin again: the user may disable them again, nothing else is lost.
            PluginHostLog.StateUnreadable(logger, exception);
        }
    }

    private void WriteState()
    {
        if (Locations.StateFile is not { } path)
        {
            return;
        }

        try
        {
            PluginStoreFiles.WriteState(path, _records.Values, _appliedRequests);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Requests not marked as applied are applied again at the next start: enabling, disabling and
            // uninstalling twice give the same result.
            PluginHostLog.StateNotWritten(logger, exception);
        }
    }

    private void DeleteStaleTemporaryStates()
    {
        if (Locations.StateFile is not { } path)
        {
            return;
        }

        try
        {
            PluginStoreFiles.DeleteStaleTemporaryStates(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Only space on the disk: the state itself is the old one or the new one, never half written.
            PluginHostLog.TemporaryStatesNotDeleted(logger, exception);
        }
    }

    // Removals that stopped half way, at the previous start: what is left is no plugin any more.
    private void DeleteUnfinishedRemovals()
    {
        if (Locations.InstalledDirectory is not { } installed || !Directory.Exists(installed))
        {
            return;
        }

        string[] leftovers;
        try
        {
            leftovers = [.. Directory.EnumerateDirectories(installed, REMOVAL_PREFIX + "*")];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            PluginHostLog.RemovalNotFinished(logger, exception);
            return;
        }

        // Each one on its own: a leftover locked by another program does not keep the others.
        foreach (var leftover in leftovers)
        {
            try
            {
                DeleteTree(leftover);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                PluginHostLog.RemovalNotFinished(logger, exception);
            }
        }
    }

    // Deletes a folder and what it holds. A link or a junction inside is removed as a link, never followed: what it
    // points to is not the plugin's. A file marked read-only, which Windows refuses to delete as is, is deleted too.
    private static void DeleteTree(string folder)
    {
        var directory = new DirectoryInfo(folder);
        if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            directory.Delete();
            return;
        }

        foreach (var file in directory.EnumerateFiles())
        {
            file.Attributes = FileAttributes.Normal;
            file.Delete();
        }

        foreach (var subfolder in directory.EnumerateDirectories())
        {
            DeleteTree(subfolder.FullName);
        }

        directory.Attributes = FileAttributes.Directory;
        directory.Delete();
    }

    private string? FolderOf(PluginKey plugin) =>
        Locations.RootOf(plugin.Origin) is { } root && Directory.Exists(Path.Combine(root, plugin.Id))
            ? Path.Combine(root, plugin.Id)
            : null;

    private PluginRequestFailure? Apply(PluginRequest request)
    {
        switch (request.Action)
        {
            case PluginRequestAction.Enable:
            case PluginRequestAction.Disable:
                if (FolderOf(request.Plugin) is null)
                {
                    return PluginRequestFailure.NotFound;
                }

                var record = _records.GetValueOrDefault(request.Plugin) ?? DefaultRecord(request.Plugin);
                _records[request.Plugin] = record with { Enabled = request.Action == PluginRequestAction.Enable };
                return null;

            case PluginRequestAction.Uninstall:
                return Uninstall(request.Plugin);

            default:
                // Installing comes with packages (ADR-0016, decision 2).
                return PluginRequestFailure.NotSupported;
        }
    }

    private PluginRequestFailure? Uninstall(PluginKey plugin)
    {
        if (plugin.Origin != PluginOrigin.Installed)
        {
            return PluginRequestFailure.NotAllowed;
        }

        if (FolderOf(plugin) is not { } folder)
        {
            return PluginRequestFailure.NotFound;
        }

        var removing = Path.Combine(Path.GetDirectoryName(folder)!, $"{REMOVAL_PREFIX}{plugin.Id}-{Guid.NewGuid():N}");
        try
        {
            // Renamed first: from here on the plugin is gone, whatever happens to its files.
            Directory.Move(folder, removing);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            PluginHostLog.RequestFailedOnFiles(logger, plugin.Id, PluginRequestAction.Uninstall, exception);
            return PluginRequestFailure.FileSystemError;
        }

        _records.Remove(plugin);
        try
        {
            DeleteTree(removing);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The plugin is uninstalled; its files go at the next start.
            PluginHostLog.RemovalNotFinished(logger, exception);
        }

        return null;
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Applies the requests that were not applied yet, in the order the desktop app wrote them, then writes the
    /// state. Called once, at startup, before any plugin loads: nothing is locked by a running plugin then. Never
    /// throws because of the files: a request that cannot be applied is kept on the plugin as its last error.
    /// </summary>
    public void ApplyRequests()
    {
        if (Locations.UserDirectory is null)
        {
            return;
        }

        DeleteUnfinishedRemovals();
        DeleteStaleTemporaryStates();
        ReadState();
        if (ReadRequests() is not { } requests)
        {
            // Nothing is known of the requests: none is applied, and those already applied stay remembered.
            WriteState();
            return;
        }

        // Only the requests still in the file are remembered: the desktop app removes those that were applied.
        var stillPending = requests.Select(request => request.Id).ToHashSet(StringComparer.Ordinal);
        _appliedRequests.IntersectWith(stillPending);

        foreach (var request in requests.Where(request => !_appliedRequests.Contains(request.Id)))
        {
            var failure = Apply(request);
            _appliedRequests.Add(request.Id);
            if (failure is { } reason)
            {
                PluginHostLog.RequestFailed(logger, request.Plugin.Id, request.Plugin.Origin, request.Action, reason);
                // Kept on the plugin only when it is there to show it: a plugin that is not there has nothing to show.
                if (FolderOf(request.Plugin) is not null)
                {
                    var record = _records.GetValueOrDefault(request.Plugin) ?? DefaultRecord(request.Plugin);
                    _records[request.Plugin] = record with { LastError = new PluginRequestError(request.Action, reason) };
                }
            }
            else
            {
                PluginHostLog.RequestApplied(logger, request.Plugin.Id, request.Plugin.Origin, request.Action);
                if (_records.TryGetValue(request.Plugin, out var record))
                {
                    _records[request.Plugin] = record with { LastError = null };
                }
            }
        }

        WriteState();
    }

    /// <summary>
    /// What the service remembers of a plugin.
    /// </summary>
    /// <param name="plugin">The plugin.</param>
    /// <returns>Null when it remembers nothing: the plugin is enabled and was never installed from a package.</returns>
    public PluginRecord? Find(PluginKey plugin) => _records.GetValueOrDefault(plugin);

    /// <summary>
    /// The requests written since the service started, which the next start will apply. Read from the file at
    /// each call: the desktop app writes it while the service runs.
    /// </summary>
    /// <returns>The pending requests, in the order of the file.</returns>
    public IReadOnlyList<PluginRequest> ReadPendingRequests() =>
        [.. (ReadRequests() ?? []).Where(request => !_appliedRequests.Contains(request.Id))];

    /// <summary>
    /// The request each plugin will end with at the next start: the last one written for it, since the requests are
    /// applied in order. The desktop app writes only one per plugin, the last replacing the previous one.
    /// </summary>
    /// <returns>The pending request of each plugin that has one.</returns>
    public IReadOnlyDictionary<PluginKey, PluginRequest> ReadPendingRequestByPlugin() =>
        ReadPendingRequests()
            .GroupBy(request => request.Plugin)
            .ToDictionary(group => group.Key, group => group.Last());

    #endregion Public Methods
}
