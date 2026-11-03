using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using NodaTime.Text;

using Voyager.PluginHost.Packages;

namespace Voyager.PluginHost;

/// <summary>
/// The JSON format of <c>requests.json</c> (written by the desktop app) and <c>state.json</c> (written by the
/// service). The requests come from another process: every entry is checked, and an entry that is not valid is
/// ignored alone.
/// </summary>
internal static partial class PluginStoreFiles
{
    #region Fields

    private const long MAXIMUM_FILE_BYTES = 1024 * 1024;
    private const int MAXIMUM_PACKAGE_NAME_LENGTH = 260;
    private const int MAXIMUM_REQUEST_ID_LENGTH = 64;

    // Names of the values are read as text, one entry at a time: an action or an origin that this version does not
    // know (written by a newer desktop app) leaves out its entry alone, never the whole file.
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    #endregion Fields

    #region Private Methods

    // The name of a value in the files: the name in code, in camel case ("fileSystemError").
    private static string NameOf<TValue>(TValue value)
        where TValue : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static string? ReadText(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists)
        {
            return null;
        }

        if (file.Length > MAXIMUM_FILE_BYTES)
        {
            throw new InvalidDataException("The file is larger than a file of requests or of state can be.");
        }

        // Shared for writing and deleting: the desktop app replaces the file while the service runs, and must never be
        // refused because the service is reading it.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"^[A-Za-z0-9-]+\z")]
    private static partial Regex RequestIdPattern();

    [GeneratedRegex(@"^[0-9a-f]{64}\z")]
    private static partial Regex Sha256Pattern();

    // A text property of an entry; null when it is missing or not a text, whatever it holds instead.
    private static string? TextOf(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // Each entry is read on its own, property by property: whatever another process wrote in one cannot spoil the others.
    private static RequestEntry? ToEntry(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
            ? new RequestEntry
            {
                Id = TextOf(element, "id"),
                Action = TextOf(element, "action"),
                PluginId = TextOf(element, "pluginId"),
                Origin = TextOf(element, "origin"),
                Package = TextOf(element, "package"),
                Sha256 = TextOf(element, "sha256"),
                PackageName = TextOf(element, "packageName"),
            }
            : null;

    private static RecordEntry ToEntry(PluginRecord record) => new()
    {
        Id = record.Plugin.Id,
        Origin = NameOf(record.Plugin.Origin),
        Enabled = record.Enabled,
        InstalledAt = record.InstalledAt is { } installedAt ? InstantPattern.ExtendedIso.Format(installedAt) : null,
        PackageName = record.PackageName,
        PackageSha256 = record.PackageSha256,
        LastError = record.LastError is { } error ? new ErrorEntry { Action = NameOf(error.Action), Reason = NameOf(error.Reason) } : null,
        // Written only when set: most plugins are neither.
        Suspended = record.Suspended ? true : null,
        DisabledForRecovery = record.DisabledForRecovery ? true : null,
    };

    private static KeyEntry ToEntry(PluginKey plugin) => new() { Origin = NameOf(plugin.Origin), Id = plugin.Id };

    private static PluginKey? ToKey(KeyEntry? entry) =>
        entry is { Id: { } id } && PluginManifestReader.IsValidIdentifier(id) && TryParse<PluginOrigin>(entry.Origin, out var origin)
            ? new PluginKey(origin, id)
            : null;

    // An installation names its prepared copy, the fingerprint the user confirmed, and the name of the file they chose.
    private static PluginPackageReference? ToPackage(RequestEntry entry) =>
        entry is { Package: { } stagedName, Sha256: { } sha256, PackageName: { Length: > 0 and <= MAXIMUM_PACKAGE_NAME_LENGTH } name }
        && PluginPackages.IsValidStagedName(stagedName)
        && Sha256Pattern().IsMatch(sha256)
            ? new PluginPackageReference(stagedName, sha256, name)
            : null;

    private static PluginRecord? ToRecord(RecordEntry entry) =>
        entry is { Id: { } id } && PluginManifestReader.IsValidIdentifier(id) && TryParse<PluginOrigin>(entry.Origin, out var origin)
            ? new PluginRecord(
                new PluginKey(origin, id),
                entry.Enabled ?? true,
                entry.InstalledAt is { } installedAt && InstantPattern.ExtendedIso.Parse(installedAt) is { Success: true } parsed ? parsed.Value : null,
                entry.PackageName,
                entry.PackageSha256,
                TryParseError(entry.LastError, out var error) ? error : null,
                entry.Suspended ?? false,
                entry.DisabledForRecovery ?? false)
            : null;

    private static PluginRequest? ToRequest(RequestEntry entry)
    {
        if (entry is not { Id: { Length: > 0 and <= MAXIMUM_REQUEST_ID_LENGTH } id, PluginId: { } pluginId }
            || !RequestIdPattern().IsMatch(id)
            || !PluginManifestReader.IsValidIdentifier(pluginId)
            || !TryParse<PluginRequestAction>(entry.Action, out var action)
            || !TryParse<PluginOrigin>(entry.Origin, out var origin))
        {
            return null;
        }

        // Only an installation names a package, and it cannot do without a valid one.
        var package = action == PluginRequestAction.Install ? ToPackage(entry) : null;
        return action == PluginRequestAction.Install && package is null
            ? null
            : new PluginRequest(id, action, new PluginKey(origin, pluginId), package);
    }

    private static bool TryParse<TValue>(string? name, out TValue value)
                                        where TValue : struct, Enum
    {
        foreach (var candidate in Enum.GetValues<TValue>())
        {
            if (string.Equals(NameOf(candidate), name, StringComparison.Ordinal))
            {
                value = candidate;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryParseError(ErrorEntry? entry, [NotNullWhen(true)] out PluginRequestError? error)
    {
        error = entry is not null
            && TryParse<PluginRequestAction>(entry.Action, out var action)
            && TryParse<PluginRequestFailure>(entry.Reason, out var reason)
                ? new PluginRequestError(action, reason)
                : null;
        return error is not null;
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// The text of the trace of the plugins being loaded or called, as <see cref="PluginActivity"/> writes it.
    /// </summary>
    /// <param name="loading">The plugin being loaded, if any.</param>
    /// <param name="calls">The plugins being called.</param>
    public static string ActivityText(PluginKey? loading, IEnumerable<PluginKey> calls) =>
        JsonSerializer.Serialize(
            new ActivityFile
            {
                Loading = loading is null ? null : ToEntry(loading),
                Calls = [.. calls.Select(ToEntry)],
            },
            _options);

    /// <summary>
    /// Deletes the temporary files of state that a write cut short (a power cut) left behind. A recent one is left: in
    /// development, another service that shares the folder may be writing it.
    /// </summary>
    /// <param name="path">The file of state.</param>
    /// <exception cref="IOException">A file could not be deleted.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder cannot be written to.</exception>
    public static void DeleteStaleTemporaryStates(string path)
    {
        var folder = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(folder))
        {
            return;
        }

        var stale = DateTime.UtcNow - TimeSpan.FromMinutes(10);
        foreach (var temporary in new DirectoryInfo(folder).EnumerateFiles($"{Path.GetFileName(path)}.*.tmp"))
        {
            if (temporary.LastWriteTimeUtc < stale)
            {
                temporary.Delete();
            }
        }
    }

    /// <summary>
    /// Reads the trace of the plugins being loaded or called that a run left: a crash left it, a normal stop deletes it.
    /// </summary>
    /// <param name="path">The file of activity.</param>
    /// <returns>The plugin being loaded, if any, and the plugins being called; nothing when the file does not exist.</returns>
    /// <exception cref="IOException">The file could not be read.</exception>
    /// <exception cref="JsonException">The file is not in the format of activity.</exception>
    /// <exception cref="InvalidDataException">The file is too large to be a file of activity.</exception>
    public static (PluginKey? Loading, IReadOnlyList<PluginKey> Calls) ReadActivity(string path)
    {
        var text = ReadText(path);
        if (text is null)
        {
            return (null, []);
        }

        var file = JsonSerializer.Deserialize<ActivityFile>(text, _options);
        return (ToKey(file?.Loading), [.. (file?.Calls ?? []).Select(ToKey).OfType<PluginKey>().Distinct()]);
    }

    /// <summary>
    /// Reads the requests written by the desktop app, and the request to start without the installed plugins.
    /// </summary>
    /// <param name="path">The file of requests.</param>
    /// <param name="ignored">How many entries were not valid and were left out.</param>
    /// <param name="recovery">
    /// The identifier of the request to start without the installed plugins, applied once like the others; null when
    /// there is none.
    /// </param>
    /// <returns>The valid requests, in the order of the file; none when the file does not exist.</returns>
    /// <exception cref="IOException">The file could not be read.</exception>
    /// <exception cref="JsonException">The file is not in the format of requests.</exception>
    /// <exception cref="InvalidDataException">The file is too large to be a file of requests.</exception>
    public static IReadOnlyList<PluginRequest> ReadRequests(string path, out int ignored, out string? recovery)
    {
        ignored = 0;
        recovery = null;
        var text = ReadText(path);
        if (text is null)
        {
            return [];
        }

        var file = JsonSerializer.Deserialize<RequestsFile>(text, _options);

        // A null recovery is none; any other value that is not a valid identifier is left out, and counted.
        if (file?.Recovery is { ValueKind: not JsonValueKind.Null } recoveryValue)
        {
            var id = recoveryValue.ValueKind == JsonValueKind.String ? recoveryValue.GetString() : null;
            if (id is { Length: > 0 and <= MAXIMUM_REQUEST_ID_LENGTH } && RequestIdPattern().IsMatch(id))
            {
                recovery = id;
            }
            else
            {
                ignored++;
            }
        }

        var requests = new List<PluginRequest>();
        foreach (var element in file?.Requests ?? [])
        {
            if (ToEntry(element) is { } entry && ToRequest(entry) is { } request)
            {
                requests.Add(request);
            }
            else
            {
                ignored++;
            }
        }

        return requests;
    }

    /// <summary>
    /// Reads the state the service wrote at its last start.
    /// </summary>
    /// <param name="path">The file of state.</param>
    /// <returns>The records of the plugins and the requests already applied; empty when the file does not exist.</returns>
    /// <exception cref="IOException">The file could not be read.</exception>
    /// <exception cref="JsonException">The file is not in the format of state.</exception>
    /// <exception cref="InvalidDataException">The file is too large to be a file of state.</exception>
    public static (IReadOnlyList<PluginRecord> Records, IReadOnlyList<string> AppliedRequests) ReadState(string path)
    {
        var text = ReadText(path);
        if (text is null)
        {
            return ([], []);
        }

        var file = JsonSerializer.Deserialize<StateFile>(text, _options);
        return (
            [.. (file?.Plugins ?? []).OfType<RecordEntry>().Select(ToRecord).OfType<PluginRecord>()],
            [.. (file?.AppliedRequests ?? []).OfType<string>()]);
    }

    /// <summary>
    /// Writes a trace of the plugins being loaded or called, as a run would leave it.
    /// </summary>
    /// <param name="path">The file of activity.</param>
    /// <param name="loading">The plugin being loaded, if any.</param>
    /// <param name="calls">The plugins being called.</param>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder cannot be written to.</exception>
    public static void WriteActivity(string path, PluginKey? loading, IEnumerable<PluginKey> calls)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ActivityText(loading, calls));
    }

    /// <summary>
    /// Writes the state in full or not at all: a temporary file next to it, then a replacement.
    /// </summary>
    /// <param name="path">The file of state.</param>
    /// <param name="records">The records of the plugins.</param>
    /// <param name="appliedRequests">The requests already applied, so that they are never applied twice.</param>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder cannot be written to.</exception>
    public static void WriteState(string path, IEnumerable<PluginRecord> records, IEnumerable<string> appliedRequests)
    {
        var file = new StateFile
        {
            Plugins = [.. records.Select(ToEntry)],
            AppliedRequests = [.. appliedRequests],
        };
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // A name of its own, so that two services that share the folder (in development) never write the same file.
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            // On the disk before the replacement: after a power cut, the state is the old one or the new one, never an
            // empty file, which would bring back every plugin the user disabled.
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, file, _options);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    #endregion Public Methods

    #region Classes

    private sealed class ActivityFile
    {
        #region Properties

        public List<KeyEntry?>? Calls { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public KeyEntry? Loading { get; init; }

        #endregion Properties
    }

    private sealed class ErrorEntry
    {
        #region Properties

        public string? Action { get; init; }

        public string? Reason { get; init; }

        #endregion Properties
    }

    private sealed class KeyEntry
    {
        #region Properties

        public string? Id { get; init; }
        public string? Origin { get; init; }

        #endregion Properties
    }

    private sealed class RecordEntry
    {
        #region Properties

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? DisabledForRecovery { get; init; }

        public bool? Enabled { get; init; }
        public string? Id { get; init; }

        public string? InstalledAt { get; init; }
        public ErrorEntry? LastError { get; init; }
        public string? Origin { get; init; }
        public string? PackageName { get; init; }

        public string? PackageSha256 { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Suspended { get; init; }

        #endregion Properties
    }

    private sealed class RequestEntry
    {
        #region Properties

        public string? Action { get; init; }
        public string? Id { get; init; }
        public string? Origin { get; init; }
        public string? Package { get; init; }
        public string? PackageName { get; init; }
        public string? PluginId { get; init; }
        public string? Sha256 { get; init; }

        #endregion Properties
    }

    private sealed class RequestsFile
    {
        #region Properties

        // Read as any JSON value: one of another type leaves out only itself.
        public JsonElement? Recovery { get; init; }

        public List<JsonElement>? Requests { get; init; }

        #endregion Properties
    }

    private sealed class StateFile
    {
        #region Properties

        public List<string?>? AppliedRequests { get; init; }
        public List<RecordEntry?>? Plugins { get; init; }

        #endregion Properties
    }

    #endregion Classes
}
