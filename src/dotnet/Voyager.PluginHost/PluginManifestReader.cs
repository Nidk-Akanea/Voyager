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

    private const int MAXIMUM_CONTRACTS = 32;
    private const int MAXIMUM_DEPTH = 8;
    private const int MAXIMUM_IDENTIFIER_LENGTH = 64;
    private const int MAXIMUM_PATH_LENGTH = 200;
    private const int MAXIMUM_VERSION_LENGTH = 64;

    // Names Windows gives to devices: a folder or a file with such a name is not what it seems.
    private static readonly HashSet<string> _reservedNames = new(
        ["con", "prn", "aux", "nul", .. Enumerable.Range(0, 10).SelectMany(digit => new[] { $"com{digit}", $"lpt{digit}" })],
        StringComparer.Ordinal);

    #endregion Fields

    #region Private Methods

    // A file name only: the plugin's assembly sits at the root of its folder.
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*\.dll\z")]
    private static partial Regex AssemblyFileNamePattern();

    [GeneratedRegex(@"^[a-z][a-z0-9]*(-[a-z0-9]+)*\z")]
    private static partial Regex IdentifierPattern();

    // A relative path with forward slashes, no empty or dot segment: it can only point inside ui/.
    [GeneratedRegex(@"^[A-Za-z0-9_-][A-Za-z0-9._-]*(/[A-Za-z0-9_-][A-Za-z0-9._-]*)*\.m?js\z")]
    private static partial Regex ModulePathPattern();

    // One entry of provides or requires: { "contract": "<identifier>", "version": <whole number from 1> }.
    private static ContractReference? ReadContract(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object
            || ReadString(item, "contract", MAXIMUM_IDENTIFIER_LENGTH) is not { } contract
            || !IdentifierPattern().IsMatch(contract)
            || !item.TryGetProperty("version", out var versionElement)
            || versionElement.ValueKind != JsonValueKind.Number
            || !versionElement.TryGetInt32(out var version)
            || version < 1)
        {
            return null;
        }

        return new ContractReference(contract, version);
    }

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

    // Every plugin requires the base contract, and none provides it: it is what the application gives to all.
    private static bool RequiresBaseContractWithoutProvidingIt(List<ContractReference> provides, List<ContractReference> requires) =>
        requires.Exists(contract => contract.Contract == ContractVersions.PLUGIN)
        && !provides.Exists(contract => contract.Contract == ContractVersions.PLUGIN);

    // Absent: an empty list. Present: an array of contracts, each named once, as the format requires. Whether the
    // application knows a contract is not decided here: ContractSupport does it, and an unknown one sets the plugin
    // aside with its own reason rather than as an invalid manifest.
    private static bool TryReadContracts(JsonElement root, string name, [NotNullWhen(true)] out List<ContractReference>? contracts)
    {
        contracts = [];
        if (!root.TryGetProperty(name, out var element))
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() > MAXIMUM_CONTRACTS)
        {
            contracts = null;
            return false;
        }

        foreach (var item in element.EnumerateArray())
        {
            var contract = ReadContract(item);
            if (contract is null || contracts.Exists(other => other.Contract == contract.Contract))
            {
                contracts = null;
                return false;
            }

            contracts.Add(contract);
        }

        return true;
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

    [GeneratedRegex(@"^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?\z")]
    private static partial Regex VersionPattern();

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Whether a text is a valid plugin identifier: lower-case words separated by hyphens, and not a name that
    /// Windows gives to a device, since an identifier names a folder.
    /// </summary>
    /// <param name="text">The text to check, for example the name of a plugin folder.</param>
    public static bool IsValidIdentifier(string text) =>
        text.Length <= MAXIMUM_IDENTIFIER_LENGTH && IdentifierPattern().IsMatch(text) && !_reservedNames.Contains(text);

    /// <summary>
    /// Reads a manifest. It is valid when it has an identifier and a version in the expected formats, at least
    /// one of a .NET part and an interface part, and contracts in the expected format, with the base contract
    /// among those it requires. Whether the application accepts these contracts is decided by
    /// <see cref="ContractSupport"/>.
    /// </summary>
    /// <param name="json">The content of <c>plugin.json</c>.</param>
    /// <param name="manifest">The manifest, when the content is valid.</param>
    /// <returns>True when the content is a valid manifest.</returns>
    public static bool TryRead(string json, [NotNullWhen(true)] out PluginManifest? manifest)
    {
        manifest = null;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = MAXIMUM_DEPTH,
                // A key written twice would let two readers of the same manifest see two different plugins.
                AllowDuplicateProperties = false,
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var id = ReadString(root, "id", MAXIMUM_IDENTIFIER_LENGTH);
            var version = ReadString(root, "version", MAXIMUM_VERSION_LENGTH);
            if (id is null || version is null || !IsValidIdentifier(id) || !VersionPattern().IsMatch(version))
            {
                return false;
            }

            if (!TryReadPart(root, "dotnet", "assembly", AssemblyFileNamePattern(), out var assembly)
                || !TryReadPart(root, "ui", "module", ModulePathPattern(), out var uiModule)
                || !TryReadContracts(root, "provides", out var provides)
                || !TryReadContracts(root, "requires", out var requires)
                || !RequiresBaseContractWithoutProvidingIt(provides, requires)
                || (assembly is null && uiModule is null))
            {
                return false;
            }

            manifest = new PluginManifest(id, version, assembly, uiModule, provides, requires);
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
