using System.Text.Json;

using Microsoft.Extensions.Logging;

using NodaTime;

using Voyager.PluginHost.Packages;

namespace Voyager.PluginHost;

/// <summary>
/// The management of the plugins between two starts (ADR-0016, decision 4): it reads the requests the desktop app
/// wrote after the user confirmed them, applies them before any plugin loads, and writes the state. The desktop app
/// is the only one to write the requests and the service the only one to write the state.
/// </summary>
/// <param name="locations">Where the plugins and the files that manage them are.</param>
/// <param name="contracts">The versions of contracts the application accepts, to examine a package again before installing it.</param>
/// <param name="clock">Gives the date of an installation.</param>
/// <param name="logger">Where the store writes its logs: identifiers of plugins and requests, never a path nor a file name.</param>
public sealed class PluginStore(PluginLocations locations, ContractSupport contracts, IClock clock, ILogger<PluginStore> logger)
{
    #region Fields

    // What a plugin being removed is renamed to, so that a removal that stops half way never leaves half a plugin
    // under its own name: the catalog skips such folders, and the next start deletes them.
    private const string REMOVAL_PREFIX = ".removing-";

    // What the previous version of a plugin is renamed to while the new one is put in its place: if a start stops in
    // between, the next one puts it back, or deletes it once the new one is there.
    private const string REPLACEMENT_PREFIX = ".replacing-";

    // A prepared copy that no request names is left alone this long: the desktop app may be asking the user about it.
    private static readonly TimeSpan _orphanCopyAge = TimeSpan.FromHours(1);

    // Pauses before a new attempt to move a folder that another program (an antivirus scanning new files) still holds.
    private static readonly TimeSpan[] _retryDelays = [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500)];

    private Dictionary<PluginKey, PluginRecord> _records = [];

    private HashSet<string> _appliedRequests = new(StringComparer.Ordinal);

    // The prepared copies of the installations applied at this start: deleted once the state says so, never before, so
    // that a start stopped in between installs them again instead of failing for a missing copy.
    private readonly List<string> _consumedCopies = [];

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
            PluginHostLog.RequestsUnreadable(logger, FileError.Describe(exception));
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
            PluginHostLog.StateUnreadable(logger, FileError.Describe(exception));
        }
    }

    private bool WriteState()
    {
        if (Locations.StateFile is not { } path)
        {
            return false;
        }

        try
        {
            PluginStoreFiles.WriteState(path, _records.Values, _appliedRequests);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Requests not marked as applied are applied again at the next start: enabling, disabling and
            // uninstalling twice give the same result, and an installation finds its copy again.
            PluginHostLog.StateNotWritten(logger, FileError.Describe(exception));
            return false;
        }
    }

    // Moves a folder, again a few times when another program holds one of its files for a moment.
    private static void MoveFolder(string source, string destination)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                Directory.Move(source, destination);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException && attempt < _retryDelays.Length)
            {
                Thread.Sleep(_retryDelays[attempt]);
            }
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
            PluginHostLog.TemporaryStatesNotDeleted(logger, FileError.Describe(exception));
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
            PluginHostLog.RemovalNotFinished(logger, FileError.Describe(exception));
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
                PluginHostLog.RemovalNotFinished(logger, FileError.Describe(exception));
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

            case PluginRequestAction.Install when request.Package is { } package:
                return Install(request.Plugin, package);

            default:
                return PluginRequestFailure.NotSupported;
        }
    }

    // Installs or replaces a plugin from its prepared copy, checked again from scratch: the fingerprint the user
    // confirmed, then the whole examination. The new version is complete before it takes the place of the old one.
    private PluginRequestFailure? Install(PluginKey plugin, PluginPackageReference package)
    {
        if (plugin.Origin != PluginOrigin.Installed
            || Locations.StagingDirectory is not { } staging
            || Locations.InstalledDirectory is not { } installed)
        {
            return PluginRequestFailure.NotAllowed;
        }

        var copy = Path.Combine(staging, package.StagedName);
        if (!File.Exists(copy))
        {
            return PluginRequestFailure.PackageMissing;
        }

        // The request is applied whatever its outcome: its copy has served. A new attempt prepares a new one.
        _consumedCopies.Add(copy);
        var extraction = Path.Combine(staging, $"{PluginPackages.EXTRACTION_PREFIX}{Guid.NewGuid():N}");
        string version;
        try
        {
            // The fingerprint comes from the very bytes that are extracted: the copy cannot change in between.
            var result = PluginPackageExtractor.Extract(copy, extraction, contracts, everything: true);
            if (!string.Equals(result.Sha256, package.Sha256, StringComparison.Ordinal))
            {
                return PluginRequestFailure.PackageChanged;
            }

            if (result.Refusal == PackageRefusal.NotEnoughSpace)
            {
                return PluginRequestFailure.NotEnoughSpace;
            }

            if (result.Refusal is { } refusal)
            {
                PluginHostLog.PackageRefused(logger, plugin.Id, refusal);
                return PluginRequestFailure.PackageRefused;
            }

            if (result.Manifest?.Id != plugin.Id)
            {
                PluginHostLog.PackageOfAnotherPlugin(logger, plugin.Id, result.Manifest?.Id);
                return PluginRequestFailure.PackageRefused;
            }

            version = result.Manifest.Version;
            Place(extraction, Path.Combine(installed, plugin.Id));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            PluginHostLog.RequestFailedOnFiles(logger, plugin.Id, PluginRequestAction.Install, FileError.Describe(exception));
            return PluginRequestFailure.FileSystemError;
        }
        finally
        {
            DeleteFolderQuietly(extraction);
        }

        PluginHostLog.PluginInstalled(logger, plugin.Id, version, package.Sha256);

        // A replacement keeps the choice of the user: an enabled plugin stays enabled, a disabled one disabled.
        var record = _records.GetValueOrDefault(plugin) ?? DefaultRecord(plugin);
        _records[plugin] = record with
        {
            InstalledAt = clock.GetCurrentInstant(),
            PackageName = DisplayText.Clean(FileNameOf(package.Name)),
            PackageSha256 = package.Sha256,
        };
        return null;
    }

    // The name of a file without any folder, whatever the desktop app gave: a path would show the account of the user.
    private static string FileNameOf(string name) => name[(name.LastIndexOfAny(['\\', '/', ':']) + 1)..];

    // Puts a complete plugin in its place. The previous version is renamed first and put back if the move fails.
    private void Place(string extraction, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string? previous = null;
        if (Directory.Exists(target))
        {
            previous = Path.Combine(Path.GetDirectoryName(target)!, $"{REPLACEMENT_PREFIX}{Path.GetFileName(target)}-{Guid.NewGuid():N}");
            MoveFolder(target, previous);
        }

        try
        {
            MoveFolder(extraction, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (previous is not null)
            {
                MoveFolder(previous, target);
            }

            throw;
        }

        if (previous is not null)
        {
            DeleteFolderQuietly(previous);
        }
    }

    // Replacements that stopped half way, at the previous start: the old version is put back when the new one is not
    // there, and deleted when it is.
    private void FinishUnfinishedReplacements()
    {
        if (Locations.InstalledDirectory is not { } installed || !Directory.Exists(installed))
        {
            return;
        }

        string[] leftovers;
        try
        {
            leftovers = [.. Directory.EnumerateDirectories(installed, REPLACEMENT_PREFIX + "*")];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            PluginHostLog.RemovalNotFinished(logger, FileError.Describe(exception));
            return;
        }

        foreach (var leftover in leftovers)
        {
            var name = Path.GetFileName(leftover)[REPLACEMENT_PREFIX.Length..];
            var dash = name.LastIndexOf('-');
            var target = Path.Combine(installed, dash > 0 ? name[..dash] : name);
            try
            {
                if (Directory.Exists(target))
                {
                    DeleteTree(leftover);
                }
                else
                {
                    MoveFolder(leftover, target);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                PluginHostLog.RemovalNotFinished(logger, FileError.Describe(exception));
            }
        }
    }

    // What an installation or an examination left in the staging folder: extraction folders, and prepared copies that
    // no request names any more (a cancelled confirmation, a request replaced by another one).
    private void CleanStaging(IReadOnlyList<PluginRequest> requests)
    {
        if (Locations.StagingDirectory is not { } staging || !Directory.Exists(staging))
        {
            return;
        }

        var named = requests.Select(request => request.Package?.StagedName).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var old = clock.GetCurrentInstant().ToDateTimeUtc() - _orphanCopyAge;
        try
        {
            foreach (var folder in Directory.EnumerateDirectories(staging).Where(folder => Path.GetFileName(folder).StartsWith('.')).ToList())
            {
                DeleteFolderQuietly(folder);
            }

            foreach (var copy in new DirectoryInfo(staging).EnumerateFiles("*" + PluginPackages.EXTENSION).ToList())
            {
                // A copy keeps the modification date of the file it was copied from: its creation tells when it was made.
                var made = copy.CreationTimeUtc > copy.LastWriteTimeUtc ? copy.CreationTimeUtc : copy.LastWriteTimeUtc;
                if (!named.Contains(copy.Name) && made < old)
                {
                    DeleteFileQuietly(copy.FullName);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            PluginHostLog.RemovalNotFinished(logger, FileError.Describe(exception));
        }
    }

    private void DeleteFolderQuietly(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                DeleteTree(folder);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            PluginHostLog.RemovalNotFinished(logger, FileError.Describe(exception));
        }
    }

    private void DeleteFileQuietly(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            PluginHostLog.RemovalNotFinished(logger, FileError.Describe(exception));
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
            PluginHostLog.RequestFailedOnFiles(logger, plugin.Id, PluginRequestAction.Uninstall, FileError.Describe(exception));
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
            PluginHostLog.RemovalNotFinished(logger, FileError.Describe(exception));
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
        FinishUnfinishedReplacements();
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
                // Kept on the plugin when it is there to show it, and for an installation, whose plugin may be new.
                if (FolderOf(request.Plugin) is not null || request.Action == PluginRequestAction.Install)
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

        CleanStaging(requests);
        if (WriteState())
        {
            foreach (var copy in _consumedCopies)
            {
                DeleteFileQuietly(copy);
            }
        }

        _consumedCopies.Clear();
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
