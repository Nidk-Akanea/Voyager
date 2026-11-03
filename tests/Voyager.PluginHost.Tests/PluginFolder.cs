using System.Text.Json.Nodes;

namespace Voyager.PluginHost.Tests;

/// <summary>
/// A temporary plugins folder, assembled from the test plugins of tests/plugin-fixtures, deleted afterwards.
/// </summary>
internal sealed class PluginFolder : IDisposable
{
    #region Fields

    private const string SOLUTION_FILE = "Voyager.slnx";

    /// <summary>The usual entry of provides: the diagnostic fact source, version 1.</summary>
    public const string PROVIDES_FACT_SOURCE = """{ "contract": "diagnostic-fact-source", "version": 1 }""";

    /// <summary>The usual entry of requires: the base contract, version 1.</summary>
    public const string REQUIRES_BASE = """{ "contract": "plugin", "version": 1 }""";

    #endregion Fields

    #region Public Constructors

    /// <summary>A new temporary folder.</summary>
    public PluginFolder()
        : this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"voyager-plugins-{Guid.NewGuid():N}"))
    {
    }

    /// <summary>A plugins folder at a given place, for example the installed plugins of a <see cref="UserPluginFolder"/>.</summary>
    /// <param name="path">The folder; created with the first plugin added.</param>
    public PluginFolder(string path) => Path = path;

    #endregion Public Constructors

    #region Properties

    /// <summary>The folder that holds one sub-folder per plugin.</summary>
    public string Path { get; }

    #endregion Properties

    #region Private Methods

    // The test plugins are built next to their project, in the configuration the tests run in.
    private static string FixtureOutput(string fixture) => OutputOf(System.IO.Path.Combine("tests", "plugin-fixtures", fixture));

    private static string OutputOf(string projectFolder)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        return System.IO.Path.Combine(RepositoryRoot(), projectFolder, "bin", configuration, "net10.0");
    }

    private static string RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(System.IO.Path.Combine(root.FullName, SOLUTION_FILE)))
        {
            root = root.Parent;
        }

        return root?.FullName ?? throw new InvalidOperationException($"No {SOLUTION_FILE} found above {AppContext.BaseDirectory}.");
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// A manifest in the JSON format of plugin.json.
    /// </summary>
    /// <param name="id">The identifier of the plugin.</param>
    /// <param name="assembly">The file name of its .NET part; null for none.</param>
    /// <param name="provides">The entries of provides, as JSON objects separated by commas.</param>
    /// <param name="requires">The entries of requires, as JSON objects separated by commas.</param>
    /// <param name="uiPart">The interface part, as a JSON fragment that starts with a comma; empty for none.</param>
    public static string Manifest(
        string id,
        string? assembly,
        string provides = PROVIDES_FACT_SOURCE,
        string requires = REQUIRES_BASE,
        string uiPart = "")
    {
        var dotnet = assembly is null ? string.Empty : $", \"dotnet\": {{ \"assembly\": \"{assembly}\" }}";
        return $$"""{ "id": "{{id}}", "version": "1.0.0"{{dotnet}}{{uiPart}}, "provides": [{{provides}}], "requires": [{{requires}}] }""";
    }

    /// <summary>Creates a folder without a manifest.</summary>
    public void AddEmptyFolder(string folderName) => Directory.CreateDirectory(System.IO.Path.Combine(Path, folderName));

    /// <summary>
    /// Writes a plugin.json, in the folder of a plugin that may not exist yet.
    /// </summary>
    public void AddManifest(string folderName, string content)
    {
        var folder = System.IO.Path.Combine(Path, folderName);
        Directory.CreateDirectory(folder);
        File.WriteAllText(System.IO.Path.Combine(folder, "plugin.json"), content);
    }

    /// <summary>
    /// Adds a plugin: the build output of a test plugin, and a manifest.
    /// </summary>
    /// <param name="folderName">The name of the plugin's folder.</param>
    /// <param name="fixture">The name of the test plugin project, for example "Good".</param>
    /// <param name="manifest">The content of plugin.json; null for the usual manifest of the plugin.</param>
    public void AddPlugin(string folderName, string fixture, string? manifest = null)
    {
        var folder = System.IO.Path.Combine(Path, folderName);
        Directory.CreateDirectory(folder);
        foreach (var file in Directory.EnumerateFiles(FixtureOutput(fixture)))
        {
            File.Copy(file, System.IO.Path.Combine(folder, System.IO.Path.GetFileName(file)));
        }

        AddManifest(folderName, manifest ?? Manifest(folderName, $"Voyager.PluginFixtures.{fixture}.dll"));
    }

    /// <summary>
    /// Adds a plugin that ships (plugins/&lt;id&gt;/dotnet/...): its build output and its manifest from the repository,
    /// with the version that the build injects.
    /// </summary>
    /// <param name="id">The identifier of the plugin, for example "diagnostics-clock".</param>
    /// <param name="project">The name of its project, for example "Voyager.Plugins.DiagnosticsClock".</param>
    public void AddShippedPlugin(string id, string project)
    {
        var folder = System.IO.Path.Combine(Path, id);
        Directory.CreateDirectory(folder);
        foreach (var file in Directory.EnumerateFiles(OutputOf(System.IO.Path.Combine("plugins", id, "dotnet", project))))
        {
            File.Copy(file, System.IO.Path.Combine(folder, System.IO.Path.GetFileName(file)));
        }

        var manifest = JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(RepositoryRoot(), "plugins", id, "plugin.json")))!.AsObject();
        manifest["version"] = "1.0.0";
        AddManifest(id, manifest.ToJsonString());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A plugin assembly stays locked by Windows while the test process lives (a load context is
            // not unloaded): the temporary folder is left to the operating system's cleanup.
        }
    }

    #endregion Public Methods
}
