using System.IO.Compression;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace Voyager.PluginHost.Packages;

/// <summary>
/// Extracts a package of plugin into a folder of its own, checking everything before and after (ADR-0016, decision 2):
/// the names and the kinds of the entries, the space on the disk, the bytes of each entry, the manifest, the files it
/// declares, the assemblies and the contracts. No size is limited: a plugin may bring a lot of data; only the free
/// space bounds it. Whatever the archive holds, the outcome is a refusal, never an exception, except when the disk
/// itself refuses to write.
/// </summary>
internal static class PluginPackageExtractor
{
    #region Fields

    private const long MAXIMUM_MANIFEST_BYTES = 64 * 1024;

    // Room left on the disk after the extraction, so that the rest of Voyager and of the computer can still write.
    private const long SPARE_BYTES = 64L * 1024 * 1024;

    // What a file costs on the disk beyond its bytes (a cluster, a record of the file system), however small it is.
    private const long BYTES_PER_FILE = 4096;

    // Windows errors of a full disk (ERROR_HANDLE_DISK_FULL, ERROR_DISK_FULL), as the HResult of an IOException.
    private const int HANDLE_DISK_FULL = unchecked((int)0x80070027);
    private const int DISK_FULL = unchecked((int)0x80070070);

    private const int COPY_BUFFER_BYTES = 81920;

    #endregion Fields

    #region Private Methods

    private static bool IsDiskFull(IOException exception) => exception.HResult is HANDLE_DISK_FULL or DISK_FULL;

    // Adds without ever going round: a size an archive announces may be as large as it likes.
    private static long Add(long left, long right) => left > long.MaxValue - right ? long.MaxValue : left + right;

    // Every entry is checked before anything is written: a package is extracted whole or not at all.
    private static PackageRefusal? CheckEntries(ZipArchive archive, out List<(ZipArchiveEntry Entry, string Path)> files)
    {
        files = [];
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            if (PackageEntryRules.IsLink(entry) || PackageEntryRules.SafePath(entry.FullName, out var isFolder) is not { } path)
            {
                return PackageRefusal.ForbiddenEntry;
            }

            if (isFolder ? !PackageEntryRules.IsAllowedFolder(path) : !PackageEntryRules.IsAllowedFile(path))
            {
                return PackageRefusal.ForbiddenEntry;
            }

            if (entry.Length < 0)
            {
                return PackageRefusal.Unreadable;
            }

            // Two names that differ only by case are one file on Windows: one would silently replace the other.
            if (!isFolder && !paths.Add(path))
            {
                return PackageRefusal.ForbiddenEntry;
            }

            var parent = path;
            while (parent.LastIndexOf('/') is var slash and > 0)
            {
                parent = parent[..slash];
                folders.Add(parent);
            }

            if (isFolder)
            {
                folders.Add(path);
            }
            else
            {
                files.Add((entry, path));
            }
        }

        // A file and a folder of the same name cannot both be extracted.
        if (paths.Overlaps(folders))
        {
            return PackageRefusal.ForbiddenEntry;
        }

        return paths.Contains(PackageEntryRules.MANIFEST) ? null : PackageRefusal.ManifestInvalid;
    }

    // What the disk can take: its free space minus the room kept for everything else; unbounded when it cannot tell.
    private static long SpaceFor(string destination)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(destination));
        return string.IsNullOrEmpty(root) ? long.MaxValue : new DriveInfo(root).AvailableFreeSpace - SPARE_BYTES;
    }

    // Reads an entry, at most the length the archive announces, and checks the CRC-32 it keeps: an entry whose bytes
    // are not the ones the archive describes (more of them, fewer, or others) is refused. The bytes are written only
    // when there is a target, and never beyond the budget of the disk.
    private static PackageRefusal? ReadEntry(ZipArchiveEntry entry, string? target, ref long budget, CancellationToken cancellationToken)
    {
        using var source = entry.Open();
        using var output = target is null ? null : new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[COPY_BUFFER_BYTES];
        var crc = new Crc32();
        long read = 0;
        int count;
        while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            read += count;
            if (read > entry.Length)
            {
                return PackageRefusal.Unreadable;
            }

            crc.Append(buffer.AsSpan(0, count));
            if (output is not null)
            {
                budget -= count;
                if (budget < 0)
                {
                    return PackageRefusal.NotEnoughSpace;
                }

                output.Write(buffer, 0, count);
            }
        }

        return read == entry.Length && crc.Value == entry.Crc32 ? null : PackageRefusal.Unreadable;
    }

    private static PluginManifest? ReadManifest(string folder)
    {
        var file = new FileInfo(Path.Combine(folder, PackageEntryRules.MANIFEST));
        if (file.Length > MAXIMUM_MANIFEST_BYTES
            || !PluginManifestReader.TryRead(File.ReadAllText(file.FullName), out var manifest)
            || string.Equals(manifest.Id, PluginCatalog.SERVICE_ID, StringComparison.Ordinal))
        {
            return null;
        }

        return manifest;
    }

    private static bool HasDeclaredFiles(IEnumerable<(ZipArchiveEntry Entry, string Path)> files, PluginManifest manifest)
    {
        var paths = files.Select(file => file.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (manifest.DotnetAssembly is not { } assembly || paths.Contains(assembly))
            && (manifest.UiModule is not { } module || paths.Contains($"ui/{module}"));
    }

    // A .NET assembly carries metadata; a native library does not, and cannot be checked or isolated (question Q35).
    private static bool AreManagedAssemblies(string folder)
    {
        foreach (var library in Directory.EnumerateFiles(folder, "*.dll"))
        {
            try
            {
                using var stream = File.OpenRead(library);
                using var reader = new PEReader(stream);
                if (!reader.HasMetadata)
                {
                    return false;
                }
            }
            catch (BadImageFormatException)
            {
                return false;
            }
        }

        return true;
    }

    // Writes what has to be on the disk and reads the rest. An examination writes only the files at the root (the
    // manifest, the assemblies to check): the data of a plugin may be large, and an examination installs nothing.
    private static PackageRefusal? ReadEntries(
        List<(ZipArchiveEntry Entry, string Path)> files,
        string destination,
        bool everything,
        CancellationToken cancellationToken)
    {
        var written = files
            .Where(file => file.Path != PackageEntryRules.SIGNATURE && (everything || !file.Path.Contains('/')))
            .Select(file => file.Entry)
            .ToHashSet();
        var needed = written.Aggregate(0L, (total, entry) => Add(Add(total, entry.Length), BYTES_PER_FILE));
        var budget = SpaceFor(destination);
        if (needed > budget)
        {
            return PackageRefusal.NotEnoughSpace;
        }

        Directory.CreateDirectory(destination);
        foreach (var (entry, path) in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? target = null;
            if (written.Contains(entry))
            {
                target = Path.Combine(destination, path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            }

            if (ReadEntry(entry, target, ref budget, cancellationToken) is { } refusal)
            {
                return refusal;
            }
        }

        return null;
    }

    private static PackageExtraction Refused(
        PackageRefusal refusal,
        string destination,
        PackageFingerprint fingerprint,
        PluginManifest? manifest = null,
        IReadOnlyList<ContractUse>? contracts = null)
    {
        DeleteFolder(destination);
        return new PackageExtraction(fingerprint.Sha256, fingerprint.Size, manifest, contracts ?? [], refusal);
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Deletes a folder this class extracted, if it is there. Its files are plain files: no link, no attribute. A
    /// folder that cannot be deleted now (a file still scanned by an antivirus) is left to the next start, which deletes
    /// the folders of the staging folder.
    /// </summary>
    /// <param name="folder">The folder of an extraction.</param>
    public static void DeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Left for the next start.
        }
    }

    /// <summary>
    /// Extracts a package into a new folder and checks what it holds. The package is read once: its fingerprint and
    /// its content come from the same bytes, whatever happens to the file meanwhile. When the package is refused, the
    /// folder is deleted.
    /// </summary>
    /// <param name="packagePath">The package, in the staging folder of the user's plugins.</param>
    /// <param name="destination">The folder to extract into; it must not exist yet.</param>
    /// <param name="contracts">The versions of contracts the application accepts.</param>
    /// <param name="everything">
    /// True to write every file, to install the plugin; false to write only the files at the root and read the others,
    /// to examine it.
    /// </param>
    /// <param name="cancellationToken">Stops the reading of a large package when nobody waits for it any more.</param>
    /// <returns>The fingerprint, the manifest and the contracts of the plugin, or why the package is refused.</returns>
    /// <exception cref="IOException">The package could not be read, or the disk refused a file for another reason than a lack of space.</exception>
    /// <exception cref="UnauthorizedAccessException">The package or the destination cannot be reached.</exception>
    /// <exception cref="OperationCanceledException">The cancellation was asked for.</exception>
    public static PackageExtraction Extract(
        string packagePath,
        string destination,
        ContractSupport contracts,
        bool everything,
        CancellationToken cancellationToken = default)
    {
        // Shared for reading only: nobody can change the package while it is read.
        using var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var fingerprint = new PackageFingerprint(Convert.ToHexStringLower(SHA256.HashData(stream)), stream.Length);
        stream.Position = 0;
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (CheckEntries(archive, out var files) is { } refusal)
            {
                return new PackageExtraction(fingerprint.Sha256, fingerprint.Size, null, [], refusal);
            }

            if (ReadEntries(files, destination, everything, cancellationToken) is { } readRefusal)
            {
                return Refused(readRefusal, destination, fingerprint);
            }

            if (ReadManifest(destination) is not { } manifest)
            {
                return Refused(PackageRefusal.ManifestInvalid, destination, fingerprint);
            }

            var uses = contracts.Describe(manifest);
            if (!HasDeclaredFiles(files, manifest))
            {
                return Refused(PackageRefusal.DeclaredFileMissing, destination, fingerprint, manifest, uses);
            }

            if (!AreManagedAssemblies(destination))
            {
                return Refused(PackageRefusal.ForbiddenEntry, destination, fingerprint, manifest, uses);
            }

            return uses.Any(use => !ContractSupport.IsAccepted(use.State))
                ? Refused(PackageRefusal.ContractNotSupported, destination, fingerprint, manifest, uses)
                : new PackageExtraction(fingerprint.Sha256, fingerprint.Size, manifest, uses, Refusal: null);
        }
        // A damaged archive shows up anywhere: its directory, an entry, its decompression.
        catch (InvalidDataException)
        {
            return Refused(PackageRefusal.Unreadable, destination, fingerprint);
        }
        catch (IOException exception) when (IsDiskFull(exception))
        {
            return Refused(PackageRefusal.NotEnoughSpace, destination, fingerprint);
        }
        catch (OperationCanceledException)
        {
            DeleteFolder(destination);
            throw;
        }
    }

    #endregion Public Methods

    #region Nested Types

    private sealed record PackageFingerprint(string Sha256, long Size);

    #endregion Nested Types
}
