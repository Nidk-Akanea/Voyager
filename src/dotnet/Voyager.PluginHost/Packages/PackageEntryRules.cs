using System.Buffers;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Voyager.PluginHost.Packages;

/// <summary>
/// What a package of plugin may hold (spec of the plugin management, "Garanties de sécurité", rule 4): the manifest,
/// the .NET assemblies with their companion files, interface files under <c>ui/</c>, translations under <c>i18n/</c>,
/// data of any kind under <c>data/</c>, and an optional signature. Every name is checked as Windows would read it:
/// nothing may leave the folder of the plugin, name a device or a stream, or be a link.
/// </summary>
/// <remarks><c>tools/pack-plugin.ps1</c> applies the same list when it builds a package.</remarks>
internal static partial class PackageEntryRules
{
    #region Fields

    /// <summary>The manifest, at the root of the package.</summary>
    public const string MANIFEST = "plugin.json";

    /// <summary>The optional signature, at the root of the package: checked, never extracted.</summary>
    public const string SIGNATURE = "signature.json";

    private const int MAXIMUM_PATH_LENGTH = 1024;
    private const int MAXIMUM_SEGMENT_LENGTH = 255;

    // The type of a link in the Unix mode that a zip archive keeps in the high bits of its external attributes.
    private const int UNIX_FILE_TYPE_MASK = 0xF000;
    private const int UNIX_SYMBOLIC_LINK = 0xA000;
    private const int UNIX_MODE_SHIFT = 16;

    private static readonly string[] _assemblyExtensions = [".dll", ".pdb", ".xml"];
    private static readonly string[] _interfaceExtensions = [".js", ".mjs", ".css", ".json", ".svg", ".png"];
    private static readonly string[] _translationExtensions = [".ftl"];
    // A backslash would be a separator on Windows, and a colon the start of a stream ("a.txt:hidden").
    private static readonly SearchValues<char> _forbiddenCharacters = SearchValues.Create("\\:*?\"<>|");

    // Names Windows gives to devices, whatever their extension: "con.txt" is the console too. The ports also take
    // superscript digits ("com¹").
    private static readonly HashSet<string> _deviceNames = new(
        [
            "con", "prn", "aux", "nul", "conin$", "conout$",
            .. "0123456789\u00B9\u00B2\u00B3".SelectMany(digit => new[] { $"com{digit}", $"lpt{digit}" }),
        ],
        StringComparer.OrdinalIgnoreCase);

    #endregion Fields

    #region Private Methods

    [GeneratedRegex(@"~[0-9]")]
    private static partial Regex ShortNamePattern();

    private static bool IsSafeSegment(string segment)
    {
        if (segment.Length is 0 or > MAXIMUM_SEGMENT_LENGTH || segment is "." or "..")
        {
            return false;
        }

        // Windows drops a final dot or space: "a." and "a" would be the same file.
        if (segment.EndsWith('.') || segment.EndsWith(' '))
        {
            return false;
        }

        if (segment.AsSpan().ContainsAny(_forbiddenCharacters) || segment.Any(char.IsControl))
        {
            return false;
        }

        // "LONGFI~1.JS" may be the short name Windows gave to another file of the package: two names, one file.
        if (ShortNamePattern().IsMatch(segment))
        {
            return false;
        }

        var baseName = segment.Split('.')[0].TrimEnd(' ');
        return !_deviceNames.Contains(baseName);
    }

    private static bool HasExtension(string name, string[] extensions) =>
        extensions.Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// The path of an entry once checked, with forward slashes; null when the name is not safe on Windows.
    /// </summary>
    /// <param name="entryName">The full name of the entry in the archive.</param>
    /// <param name="isFolder">Whether the entry is a folder (its name ends with a slash).</param>
    public static string? SafePath(string entryName, out bool isFolder)
    {
        isFolder = entryName.EndsWith('/');
        var path = isFolder ? entryName[..^1] : entryName;
        if (path.Length is 0 or > MAXIMUM_PATH_LENGTH || path.StartsWith('/'))
        {
            return null;
        }

        return path.Split('/').All(IsSafeSegment) ? path : null;
    }

    /// <summary>
    /// Whether an entry is a link: following it on extraction could write anywhere on the disk.
    /// </summary>
    /// <param name="entry">The entry of the archive.</param>
    public static bool IsLink(ZipArchiveEntry entry) =>
        ((entry.ExternalAttributes >> UNIX_MODE_SHIFT) & UNIX_FILE_TYPE_MASK) == UNIX_SYMBOLIC_LINK
        || ((FileAttributes)(entry.ExternalAttributes & 0xFFFF)).HasFlag(FileAttributes.ReparsePoint);

    /// <summary>
    /// Whether a file of a safe path may be in a package, at its place.
    /// </summary>
    /// <param name="path">The checked path, with forward slashes.</param>
    public static bool IsAllowedFile(string path)
    {
        var segments = path.Split('/');
        if (segments.Length == 1)
        {
            return path is MANIFEST or SIGNATURE
                || HasExtension(path, _assemblyExtensions)
                || path.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase);
        }

        return segments[0] switch
        {
            "ui" => HasExtension(path, _interfaceExtensions),
            "i18n" => HasExtension(path, _translationExtensions),
            // Data of any kind: Voyager never serves it to the interface and never runs it.
            "data" => true,
            _ => false,
        };
    }

    /// <summary>
    /// Whether a folder of a safe path may be in a package: the three folders above, and what they hold.
    /// </summary>
    /// <param name="path">The checked path, with forward slashes, without the final slash.</param>
    public static bool IsAllowedFolder(string path) => path.Split('/')[0] is "ui" or "i18n" or "data";

    #endregion Public Methods
}
