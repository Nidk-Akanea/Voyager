using System.Text.RegularExpressions;

namespace Voyager.PluginHost.Packages;

/// <summary>
/// The packages the desktop app prepared in the staging folder of the user's plugins (ADR-0016, decision 3): it
/// examines one before the user confirms its installation. An examination extracts the package into a folder of its
/// own, checks it and deletes the folder: nothing is installed, and nothing of the package runs.
/// </summary>
/// <param name="locations">Where the plugins and the files that manage them are.</param>
/// <param name="contracts">The versions of contracts the application accepts.</param>
public sealed partial class PluginPackages(PluginLocations locations, ContractSupport contracts) : IDisposable
{
    #region Fields

    /// <summary>The extension of a package of plugin.</summary>
    public const string EXTENSION = ".voyagerplugin";

    // Folders the store and the examinations create in the staging folder, deleted at the next start if left behind.
    internal const string EXTRACTION_PREFIX = ".extract-";
    internal const string EXAMINATION_PREFIX = ".examine-";

    // One examination at a time: two of them would each check the free space, then both write.
    private readonly SemaphoreSlim _examinations = new(1, 1);

    #endregion Fields

    #region Private Methods

    // The desktop app names a prepared copy itself, at random: never a path, never the name of the file the user chose,
    // which may hold a person's name and would otherwise show in the paths of the logs.
    [GeneratedRegex(@"^[0-9a-f]{32}\.voyagerplugin\z")]
    private static partial Regex StagedNamePattern();

    private static PackageInstallKind KindOf(string version, string? installedVersion) =>
        installedVersion is null
            ? PackageInstallKind.New
            : PluginVersions.Compare(version, installedVersion) switch
            {
                > 0 => PackageInstallKind.Upgrade,
                < 0 => PackageInstallKind.Downgrade,
                _ => PackageInstallKind.Reinstall,
            };

    private string? InstalledVersionOf(string pluginId)
    {
        if (locations.InstalledDirectory is not { } installed)
        {
            return null;
        }

        var manifest = Path.Combine(installed, pluginId, PackageEntryRules.MANIFEST);
        try
        {
            return File.Exists(manifest) && PluginManifestReader.TryRead(File.ReadAllText(manifest), out var read) ? read.Version : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private bool IsShipped(string pluginId) =>
        locations.ShippedDirectory is { } shipped && Directory.Exists(Path.Combine(shipped, pluginId));

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Whether a text is the name of a prepared copy: 32 lower-case hexadecimal characters chosen at random by the
    /// desktop app, and the extension of a package.
    /// </summary>
    /// <param name="stagedName">The name to check.</param>
    public static bool IsValidStagedName(string stagedName) => StagedNamePattern().IsMatch(stagedName);

    /// <summary>
    /// The path of a prepared copy, or null when the name is not one or when plugins cannot be managed.
    /// </summary>
    /// <param name="stagedName">The name of the prepared copy.</param>
    public string? PathOf(string stagedName) =>
        IsValidStagedName(stagedName) && locations.StagingDirectory is { } staging ? Path.Combine(staging, stagedName) : null;

    /// <summary>
    /// Examines a prepared copy: what it holds and what installing it would do.
    /// </summary>
    /// <param name="stagedName">The name of the prepared copy in the staging folder.</param>
    /// <param name="cancellationToken">Stops the examination of a large package when nobody waits for it any more.</param>
    /// <returns>What the examination found.</returns>
    /// <exception cref="InvalidOperationException">Plugins cannot be managed: there is no staging folder.</exception>
    /// <exception cref="ArgumentException">The name is not the name of a prepared copy.</exception>
    /// <exception cref="FileNotFoundException">There is no such prepared copy.</exception>
    /// <exception cref="IOException">The copy, or the folder to examine it in, could not be read or written.</exception>
    /// <exception cref="UnauthorizedAccessException">The staging folder cannot be written to.</exception>
    /// <exception cref="OperationCanceledException">The cancellation was asked for.</exception>
    public async Task<PackageExamination> ExamineAsync(string stagedName, CancellationToken cancellationToken)
    {
        if (locations.StagingDirectory is null)
        {
            throw new InvalidOperationException("Plugins cannot be managed: there is no staging folder.");
        }

        var path = PathOf(stagedName) ?? throw new ArgumentException("This is not the name of a prepared package.", nameof(stagedName));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"There is no prepared package {stagedName}.");
        }

        await _examinations.WaitAsync(cancellationToken);
        var folder = Path.Combine(Path.GetDirectoryName(path)!, $"{EXAMINATION_PREFIX}{Guid.NewGuid():N}");
        try
        {
            var extraction = PluginPackageExtractor.Extract(path, folder, contracts, everything: false, cancellationToken);
            var manifest = extraction.Manifest;
            var installedVersion = manifest is null ? null : InstalledVersionOf(manifest.Id);
            return new PackageExamination(
                extraction.Sha256,
                extraction.Size,
                extraction.Refusal,
                manifest,
                extraction.Contracts,
                DuplicateOfShipped: manifest is not null && IsShipped(manifest.Id),
                installedVersion,
                manifest is null ? PackageInstallKind.New : KindOf(manifest.Version, installedVersion));
        }
        finally
        {
            PluginPackageExtractor.DeleteFolder(folder);
            _examinations.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _examinations.Dispose();

    #endregion Public Methods
}
