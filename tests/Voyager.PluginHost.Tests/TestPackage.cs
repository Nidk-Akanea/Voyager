using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Voyager.PluginHost.Tests;

/// <summary>
/// One entry of a test package: its name in the archive, its content and, for a link, the attributes that say so.
/// </summary>
/// <param name="Name">The full name of the entry, with forward slashes; a folder ends with a slash.</param>
/// <param name="Content">The bytes of the entry; empty for a folder.</param>
/// <param name="ExternalAttributes">The external attributes of the entry; zero for a plain file.</param>
internal sealed record PackageEntry(string Name, byte[] Content, int ExternalAttributes = 0)
{
    /// <summary>A text file.</summary>
    public static PackageEntry Text(string name, string content) => new(name, Encoding.UTF8.GetBytes(content));

    /// <summary>A folder entry; its name gets a final slash.</summary>
    public static PackageEntry Folder(string name) => new(name.EndsWith('/') ? name : name + "/", []);
}

/// <summary>
/// One entry of a hand-written test package, stored without compression: what <see cref="ZipArchive"/> never writes,
/// such as a size announced in a Zip64 field or a wrong CRC-32.
/// </summary>
/// <param name="Name">The full name of the entry, with forward slashes.</param>
/// <param name="Content">The bytes the archive holds for the entry.</param>
/// <param name="AnnouncedLength">The size the entry announces in a Zip64 field; null for its true size.</param>
/// <param name="WrongCrc">Whether the archive keeps a CRC-32 that is not the one of the content.</param>
internal sealed record RawPackageEntry(string Name, byte[] Content, long? AnnouncedLength = null, bool WrongCrc = false)
{
    /// <summary>A text file.</summary>
    public static RawPackageEntry Text(string name, string content) => new(name, Encoding.UTF8.GetBytes(content));
}

/// <summary>
/// Builds .voyagerplugin packages in memory for the tests, from fictional plugins or from the build output of a test
/// plugin, and writes them where the desktop app would put its prepared copies.
/// </summary>
internal static class TestPackage
{
    #region Fields

    /// <summary>The interface module of the plugins built here.</summary>
    public const string UI_MODULE = "index.js";

    /// <summary>The usual entry of requires: the base contract, version 1.</summary>
    public const string REQUIRES_BASE = """{ "contract": "plugin", "version": 1 }""";

    private const uint LOCAL_HEADER_SIGNATURE = 0x04034B50;
    private const uint CENTRAL_HEADER_SIGNATURE = 0x02014B50;
    private const uint END_OF_CENTRAL_DIRECTORY_SIGNATURE = 0x06054B50;
    private const ushort ZIP64_EXTRA_FIELD = 0x0001;
    private const ushort UTF8_NAMES = 0x0800;

    // 1980-01-01, the first date a zip archive can hold.
    private const ushort FIRST_DOS_DATE = (1 << 5) | 1;

    #endregion Fields

    #region Private Methods

    // The CRC-32 of zip archives, computed apart from the code under test.
    private static uint Crc32Of(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }

    // The fields every header starts with: signature, versions, flags, method (stored), time and date.
    private static void WriteHeaderStart(BinaryWriter writer, uint signature, bool zip64, bool central)
    {
        var version = zip64 ? (ushort)45 : (ushort)20;
        writer.Write(signature);
        if (central)
        {
            writer.Write(version);
        }

        writer.Write(version);
        writer.Write(UTF8_NAMES);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(FIRST_DOS_DATE);
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// A name of prepared copy as the desktop app chooses it: 32 random lower-case hexadecimal characters.
    /// </summary>
    public static string NewStagedName() => $"{Guid.NewGuid():N}.voyagerplugin";

    /// <summary>
    /// A manifest with an interface part only, unless an assembly is given.
    /// </summary>
    /// <param name="id">The identifier of the plugin.</param>
    /// <param name="version">Its version.</param>
    /// <param name="requires">The entries of requires, as JSON objects separated by commas.</param>
    /// <param name="assembly">The file name of its .NET part; null for none.</param>
    /// <param name="uiModule">Its interface module; null for none.</param>
    /// <param name="provides">The entries of provides, as JSON objects separated by commas.</param>
    public static string Manifest(
        string id,
        string version = "1.0.0",
        string requires = REQUIRES_BASE,
        string? assembly = null,
        string? uiModule = UI_MODULE,
        string provides = "")
    {
        var dotnet = assembly is null ? string.Empty : $", \"dotnet\": {{ \"assembly\": \"{assembly}\" }}";
        var ui = uiModule is null ? string.Empty : $", \"ui\": {{ \"module\": \"{uiModule}\" }}";
        return $$"""{ "id": "{{id}}", "version": "{{version}}"{{dotnet}}{{ui}}, "provides": [{{provides}}], "requires": [{{requires}}] }""";
    }

    /// <summary>
    /// The entries of a valid plugin with an interface part only: its manifest and its module.
    /// </summary>
    /// <param name="id">The identifier of the plugin.</param>
    /// <param name="version">Its version.</param>
    /// <param name="requires">The entries of requires.</param>
    public static List<PackageEntry> View(string id, string version = "1.0.0", string requires = REQUIRES_BASE) =>
    [
        PackageEntry.Text("plugin.json", Manifest(id, version, requires)),
        PackageEntry.Text($"ui/{UI_MODULE}", "export default {};"),
    ];

    /// <summary>
    /// The entries of every file of a plugin folder, for example one assembled by <see cref="PluginFolder.AddPlugin"/>.
    /// </summary>
    /// <param name="folder">The folder of the plugin.</param>
    public static List<PackageEntry> FromFolder(string folder) =>
    [
        .. Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(file => new PackageEntry(Path.GetRelativePath(folder, file).Replace('\\', '/'), File.ReadAllBytes(file))),
    ];

    /// <summary>
    /// Writes a package with these entries, in this order.
    /// </summary>
    /// <param name="path">The file to write; its folder is created.</param>
    /// <param name="entries">The entries of the archive.</param>
    /// <returns>The path of the package.</returns>
    public static string Write(string path, IEnumerable<PackageEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var entry in entries)
            {
                var zipEntry = archive.CreateEntry(entry.Name, CompressionLevel.Optimal);
                if (entry.ExternalAttributes != 0)
                {
                    zipEntry.ExternalAttributes = entry.ExternalAttributes;
                }

                using var stream = zipEntry.Open();
                stream.Write(entry.Content);
            }
        }

        return path;
    }

    /// <summary>
    /// Writes a package by hand, entry by entry, stored without compression, to describe what a well-behaved zip
    /// library never writes.
    /// </summary>
    /// <param name="path">The file to write; its folder is created.</param>
    /// <param name="entries">The entries of the archive.</param>
    /// <returns>The path of the package.</returns>
    public static string WriteRaw(string path, IEnumerable<RawPackageEntry> entries)
    {
        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer);
        var central = new List<(RawPackageEntry Entry, byte[] Name, uint Crc, uint Offset)>();
        foreach (var entry in entries)
        {
            var name = Encoding.UTF8.GetBytes(entry.Name);
            var crc = Crc32Of(entry.Content) ^ (entry.WrongCrc ? 1u : 0u);
            var zip64 = entry.AnnouncedLength is not null;
            central.Add((entry, name, crc, (uint)buffer.Position));

            WriteHeaderStart(writer, LOCAL_HEADER_SIGNATURE, zip64, central: false);
            writer.Write(crc);
            writer.Write(zip64 ? uint.MaxValue : (uint)entry.Content.Length);
            writer.Write(zip64 ? uint.MaxValue : (uint)entry.Content.Length);
            writer.Write((ushort)name.Length);
            writer.Write(zip64 ? (ushort)20 : (ushort)0);
            writer.Write(name);
            if (entry.AnnouncedLength is { } announced)
            {
                writer.Write(ZIP64_EXTRA_FIELD);
                writer.Write((ushort)16);
                writer.Write(announced);
                writer.Write((long)entry.Content.Length);
            }

            writer.Write(entry.Content);
        }

        var centralStart = (uint)buffer.Position;
        foreach (var (entry, name, crc, offset) in central)
        {
            var zip64 = entry.AnnouncedLength is not null;
            WriteHeaderStart(writer, CENTRAL_HEADER_SIGNATURE, zip64, central: true);
            writer.Write(crc);
            writer.Write((uint)entry.Content.Length);
            writer.Write(zip64 ? uint.MaxValue : (uint)entry.Content.Length);
            writer.Write((ushort)name.Length);
            writer.Write(zip64 ? (ushort)12 : (ushort)0);
            // Comment length, disk, internal and external attributes.
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write(0u);
            writer.Write(offset);
            writer.Write(name);
            if (entry.AnnouncedLength is { } announced)
            {
                writer.Write(ZIP64_EXTRA_FIELD);
                writer.Write((ushort)8);
                writer.Write(announced);
            }
        }

        var centralSize = (uint)buffer.Position - centralStart;
        writer.Write(END_OF_CENTRAL_DIRECTORY_SIGNATURE);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)central.Count);
        writer.Write((ushort)central.Count);
        writer.Write(centralSize);
        writer.Write(centralStart);
        writer.Write((ushort)0);
        writer.Flush();

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, buffer.ToArray());
        return path;
    }

    /// <summary>
    /// Breaks the first header of the central directory of a package and leaves its end record as it was: the
    /// archive opens, and its list of entries cannot be read.
    /// </summary>
    /// <param name="path">The package.</param>
    public static void DamageCentralDirectory(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var end = bytes.AsSpan().LastIndexOf(BitConverter.GetBytes(END_OF_CENTRAL_DIRECTORY_SIGNATURE));
        var centralStart = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(end + 16));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(centralStart), 0xDEADBEEF);
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>
    /// Writes a package as a prepared copy, in the staging folder of the user's plugins.
    /// </summary>
    /// <param name="user">The plugins folder of the user's data.</param>
    /// <param name="stagedName">The name the desktop app gave the copy.</param>
    /// <param name="entries">The entries of the archive.</param>
    /// <returns>The path of the prepared copy.</returns>
    public static string Stage(UserPluginFolder user, string stagedName, IEnumerable<PackageEntry> entries) =>
        Write(Path.Combine(user.StagingDirectory, stagedName), entries);

    /// <summary>
    /// Writes a file of any content as a prepared copy, for example random bytes that are no archive.
    /// </summary>
    /// <returns>The path of the prepared copy.</returns>
    public static string StageBytes(UserPluginFolder user, string stagedName, byte[] content)
    {
        Directory.CreateDirectory(user.StagingDirectory);
        var path = Path.Combine(user.StagingDirectory, stagedName);
        File.WriteAllBytes(path, content);
        return path;
    }

    /// <summary>
    /// The SHA-256 fingerprint of a file in lower-case hexadecimal, computed apart from the code under test.
    /// </summary>
    public static string Sha256Of(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    #endregion Public Methods
}
