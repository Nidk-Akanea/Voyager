using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;

using Voyager.Sdk;

namespace Voyager.PluginHost;

/// <summary>
/// Reads and validates a <c>plugin.json</c> file. A manifest that is unreadable or incomplete is
/// refused whole: the host never guesses what a plugin meant.
/// </summary>
public static partial class PluginManifestReader
{
    #region Fields

    private const int MAXIMUM_DEPTH = 8;
    private const int MAXIMUM_IDENTIFIER_LENGTH = 64;
    private const int MAXIMUM_VERSION_LENGTH = 64;
    private const int MAXIMUM_PATH_LENGTH = 200;

    private static readonly string[] _knownExtensionPoints = [ExtensionPoints.DIAGNOSTIC_FACT_SOURCE];

    #endregion Fields

    #region Private Methods

    [GeneratedRegex(@"^[a-z][a-z0-9]*(-[a-z0-9]+)*\z")]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?\z")]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"^\d+\.\d+\z")]
    private static partial Regex SdkVersionPattern();

    // A file name only: the plugin's assembly sits at the root of its folder.
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*\.dll\z")]
    private static partial Regex AssemblyFileNamePattern();

    // A relative path with forward slashes, no empty or dot segment: it can only point inside ui/.
    [GeneratedRegex(@"^[A-Za-z0-9_-][A-Za-z0-9._-]*(/[A-Za-z0-9_-][A-Za-z0-9._-]*)*\.m?js\z")]
    private static partial Regex ModulePathPattern();

    private static string? ReadString(JsonElement parent, string name, int maximumLength)
    {
        if (!parent.TryGetProperty(name, out var element)
            || element.ValueKind != JsonValueKind.String
            || element.GetString() is not { Length: > 0 } text
            || text.Length > maximumLength)
        {
            return null;
        }

        return text;
    }

    // Absent: fine, the plugin has no such part. Present: it must be a complete object.
    private static bool TryReadPart(JsonElement root, string part, string field, Regex pattern, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(part, out var element))
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        value = ReadString(element, field, MAXIMUM_PATH_LENGTH);
        return value is not null && pattern.IsMatch(value);
    }

    private static bool TryReadProvides(JsonElement root, [NotNullWhen(true)] out List<string>? provides)
    {
        provides = [];
        if (!root.TryGetProperty("provides", out var element))
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            provides = null;
            return false;
        }

        foreach (var item in element.EnumerateArray())
        {
            var point = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (point is null || !_knownExtensionPoints.Contains(point, StringComparer.Ordinal) || provides.Contains(point))
            {
                provides = null;
                return false;
            }

            provides.Add(point);
        }

        return true;
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Whether a text is a valid plugin identifier: lower-case words separated by hyphens.
    /// </summary>
    /// <param name="text">The text to check, for example the name of a plugin folder.</param>
    public static bool IsValidIdentifier(string text) =>
        text.Length <= MAXIMUM_IDENTIFIER_LENGTH && IdentifierPattern().IsMatch(text);

    /// <summary>
    /// Reads a manifest. It is valid when it has an identifier, a version and an SDK version in the
    /// expected formats, at least one of a .NET part and an interface part, and only known extension points.
    /// </summary>
    /// <param name="json">The content of <c>plugin.json</c>.</param>
    /// <param name="manifest">The manifest, when the content is valid.</param>
    /// <returns>True when the content is a valid manifest.</returns>
    public static bool TryRead(string json, [NotNullWhen(true)] out PluginManifest? manifest)
    {
        manifest = null;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = MAXIMUM_DEPTH });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var id = ReadString(root, "id", MAXIMUM_IDENTIFIER_LENGTH);
            var version = ReadString(root, "version", MAXIMUM_VERSION_LENGTH);
            var sdk = ReadString(root, "sdk", MAXIMUM_VERSION_LENGTH);
            if (id is null || version is null || sdk is null
                || !IdentifierPattern().IsMatch(id)
                || !VersionPattern().IsMatch(version)
                || !SdkVersionPattern().IsMatch(sdk))
            {
                return false;
            }

            if (!TryReadPart(root, "dotnet", "assembly", AssemblyFileNamePattern(), out var assembly)
                || !TryReadPart(root, "ui", "module", ModulePathPattern(), out var uiModule)
                || !TryReadProvides(root, out var provides)
                || (assembly is null && uiModule is null))
            {
                return false;
            }

            manifest = new PluginManifest(id, version, sdk, assembly, uiModule, provides);
            return true;
        }
        catch (JsonException)
        {
            // Not valid JSON: refused like any other invalid manifest.
            return false;
        }
    }

    #endregion Public Methods
}
